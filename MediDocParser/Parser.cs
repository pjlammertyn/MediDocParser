using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MediDocParser.Model;

namespace MediDocParser
{
    public class Parser
    {
        #region Fields

        int lineNumber;

        #endregion

        #region Constructor

        public Parser()
        {
            ParserErrors = new Dictionary<int, IList<string>>();
        }

        #endregion

        #region Properties

        public IDictionary<int, IList<string>> ParserErrors { get; set; }

        #endregion

        #region Parser Methods

        public IEnumerable<ExecutingDoctor> ParseTextReport(string text)
        {
            if (text == null)
                throw new ArgumentNullException("text");

            using (var reader = new StringReader(text))
            {
                return ParseTextReport(reader);
            }
        }

        public IEnumerable<ExecutingDoctor> ParseTextReport(TextReader reader)
        {
            var executingDoctors = new List<ExecutingDoctor>();
            lineNumber = -1;
            ParserErrors.Clear();

            var line = ReadLine(reader);
            do
            {
                if (line != null)
                    executingDoctors.Add(ParseTextReportDoctorBlock(reader, line));
            }
            while ((line = ReadLine(reader)) != null);

            return executingDoctors;
        }

        public IEnumerable<Lab> ParseLabReport(string text)
        {
            if (text == null)
                throw new ArgumentNullException("text");

            using (var reader = new StringReader(text))
            {
                return ParseLabReport(reader);
            }
        }

        public IEnumerable<Lab> ParseLabReport(TextReader reader)
        {
            var labs = new List<Lab>();
            lineNumber = -1;
            ParserErrors.Clear();

            var line = ReadLine(reader);
            do
            {
                if (line != null)
                    labs.Add(ParseLabBlock(reader, line));
            }
            while ((line = ReadLine(reader)) != null);

            return labs;
        }

        #endregion

        #region Private Parser Methods

        Lab ParseLabBlock(TextReader reader, string line)
        {
            var lab = new Lab();

            //Lijn 1: “Medidoc” identificatienummer van het laboratorium. 
            //formaat: 4 karakters en wordt als volgt gevormd: de eerste letter van de provincie (W,O,A,B,L) gevolgd door de eerste twee cijfers van de postkode, gevolgd door een volgnummer binnen de stad of gemeente. (vb. W841 voor een labo te Oostende)
            if (line == null || !Regex.Match(line.Trim(), @"[WOABL]\d{3}", RegexOptions.IgnoreCase).Success)
                ParserErrors.AddItem(lineNumber, $"Not valid start of lab block expected W,O,A,B,L followed by three digits but got '{line}'");
            lab.Id = line?.Trim();

            //Lijn 2 6: Identificatiegegevens van het labo (naam, adres, tel ...)
            //formaat: vrije tekst met maximaal 50 karakters per lijn .
            lab.Name = ReadLine(reader)?.CheckMaxSize(50, ParserErrors, lineNumber).Trim();
            lab.Address1 = ReadLine(reader)?.CheckMaxSize(50, ParserErrors, lineNumber).Trim();
            lab.Address2 = ReadLine(reader)?.CheckMaxSize(50, ParserErrors, lineNumber).Trim();
            lab.IdentificationData1 = ReadLine(reader)?.CheckMaxSize(50, ParserErrors, lineNumber).Trim();
            lab.IdentificationData2 = ReadLine(reader)?.CheckMaxSize(50, ParserErrors, lineNumber).Trim();

            //Lijn 7: datum (+ eventueel tijdstip) aanmaak
            //formaat: JJJJMMDD(+evtHHMM)
            lab.Date = ReadLine(reader)?.ToNullableDatetime("yyyyMMddHHmm", "yyyyMMdd", "yyMMdd");

            lab.RequestingDoctor = new Doctor();
            //Lijn 8: RIZIV nummer aanvragende arts
            //formaat: C/CCCCC/CC/CCC
            line = ReadLine(reader);
            if (line == null || !Regex.Match(line.Trim(), @"\d/\d{5}/\d{2}/\d{3}", RegexOptions.IgnoreCase).Success)
                ParserErrors.AddItem(lineNumber, $"Not a valid rizivnumber: '{line}' of format C/CCCCC/CC/CCC");
            lab.RequestingDoctor.RizivNr = line?.Replace("/", string.Empty).Trim();

            //lijn 9: Naam (positie 1-24) + Voornaam (positie 25-40) aanvragende arts
            var name = ReadLine(reader)?.CheckMaxSize(40, ParserErrors, lineNumber);
            lab.RequestingDoctor.LastName = name?.Substring(0, name.Length > 24 ? 24 : name.Length).Trim();
            if (name?.Length > 24)
                lab.RequestingDoctor.FirstName = name?.Substring(24).Trim();

            line = ReadLine(reader);
            do
            {
                if (line == null || !line.StartsWith("#A"))
                {
                    ParserErrors.AddItem(lineNumber, $"Not valid start of patient block expected '#A' but got '{line}'");
                    break;
                }

                if (line != null)
                    lab.Patients.Add(ParsePatientBlock(reader, line, true));
            }
            while ((line = ReadLine(reader)) != null && !line.StartsWith(@"#/"));

            if (line == null || !line.StartsWith(@"#/"))
                ParserErrors.AddItem(lineNumber, $"Expected end of lab blok '#/' but got '{line}'");

            return lab;
        }

        Patient ParsePatientBlock(TextReader reader, string firstLine, bool lab)
        {
            var patient = new Patient();

            //Lijn 1: aanduiding begin van een aanvraag formaat: #A (eventueel gevolgd het rijksregisternummer van de patient of bij gebrek hieraan het Medidoc dossiernummer van de patiënt   zie appendix A voor de vorming van het Medidoc dossiernummer)
            if (firstLine.Length > 2)
            {
                var id = firstLine.Substring(2);
                if (id.Length == 11)
                {
                    if (!id.IsValidSocialSecurityNumber())
                        ParserErrors.AddItem(lineNumber, $"Not a valid SSN: '{id}'");
                }
                else if (id.Length == 13)
                {
                    if (id == null || !Regex.Match(id.Trim(), @"\d{6}[XYZ][A-Z.]{6}", RegexOptions.IgnoreCase).Success)
                        ParserErrors.AddItem(lineNumber, $"Not a valid Medidoc dossiernummer: '{id}'");
                }
                else
                {
                    ParserErrors.AddItem(lineNumber, $"Invalid patient id '{firstLine}'");
                }

                patient.Id = id?.Trim();
            }

            //Lijn 2:	naam en voornaam van de patiënt
            var name = ReadLine(reader)?.CheckMaxSize(40, ParserErrors, lineNumber);
            patient.LastName = name?.Substring(0, name.Length > 24 ? 24 : name.Length).Trim();
            if (name?.Length > 24)
                patient.FirstName = name?.Substring(24).Trim();

            //Lijn 3: geboortedatum patiënt
            //formaat: JJJJMMDD
            patient.BirthDate = ReadLine(reader)?.CheckMaxSize(8, ParserErrors, lineNumber).ToNullableDatetime("yyyyMMdd", "yyMMdd");

            //Lijn 4: geslacht patiënt
            //formaat: X, of Y, of Z
            switch (ReadLine(reader)?.CheckMaxSize(1, ParserErrors, lineNumber))
            {
                case "X":
                    patient.Sex = Sex.female;
                    break;
                case "Y":
                    patient.Sex = Sex.male;
                    break;
                default:
                    patient.Sex = Sex.unknown;
                    break;
            }

            //Lijn 5:	datum van de aanvraag
            //formaat: JJJJMMDD
            patient.RequestDate = ReadLine(reader)?.CheckMaxSize(8, ParserErrors, lineNumber).ToNullableDatetime("yyyyMMdd", "yyMMdd");

            //(Lijn 6:	referentienummer aanvraag 
            //formaat: max 14 karakters.
            patient.ReferenceNumber = ReadLine(reader)?.CheckMaxSize(14, ParserErrors, lineNumber).Trim();

            if (lab)
            {
                //Lijn 7:	protocol code
                //formaat: 1 karakter, zijnde: P indien partieel protocol; C indien volledig protocol; S indien aanvulling van een partieel; L indien het de laatste aanvulling is
                var protocolCode = ReadLine(reader)?.CheckMaxSize(1, ParserErrors, lineNumber);
                switch (protocolCode)
                {
                    case "P":
                        patient.ProtocolCode = ProtocolCode.Partial;
                        break;
                    case "C":
                        patient.ProtocolCode = ProtocolCode.Full;
                        break;
                    case "S":
                        patient.ProtocolCode = ProtocolCode.Adition;
                        break;
                    case "L":
                        patient.ProtocolCode = ProtocolCode.LastAdition;
                        break;
                    default:
                        ParserErrors.AddItem(lineNumber, string.Format($"'{protocolCode}' is not a valid protocol code (only P,C,S and L) are allowed."));
                        break;
                }
            }
            else
            {
                //(lijn 7:)Episodenummer (positie 1-14); legt verband tussen meerdere onderzoeken
                //mag blanco gelaten worden
                patient.EpisodeNumber = ReadLine(reader)?.CheckMaxSize(14, ParserErrors, lineNumber).Trim();
            }

            //(lijn 1-7 zijn obligaat, de volgende lijnen mogen weggelaten worden)
            var line = ReadLine(reader);
            if (line != null && !line.StartsWith(@"#R"))
            {
                //(lijn 8:)Straat (positie 1-24) + nr (positie 25-31)
                var address = line?.CheckMaxSize(31, ParserErrors, lineNumber);
                patient.Address.Street = address?.Substring(0, address.Length > 35 ? 35 : address.Length).Trim();
                if (address?.Length > 35)
                    patient.Address.HouseNr = address?.Substring(35).Trim();

                line = ReadLine(reader);
                if (line != null && !line.StartsWith(@"#R"))
                {
                    //(lijn 9:)Postcode (positie 1-7)
                    patient.Address.PostalCode = line?.CheckMaxSize(7, ParserErrors, lineNumber).Trim();

                    line = ReadLine(reader);
                    if (line != null && !line.StartsWith(@"#R"))
                    {
                        //(lijn 10:)Gemeente (positie 1-24)
                        patient.Address.Town = line?.CheckMaxSize(24, ParserErrors, lineNumber).Trim();
                    }
                }
            }
            if (line != null && !line.StartsWith(@"#R"))
            {
                //(lijn 11 en volgende: in voorbereiding voor mut-gegevens, enz)
                while ((line = ReadLine(reader)) != null && !line.StartsWith(@"#R"))
                { }
            }

            do
            {
                if (line == null || !line.StartsWith("#R"))
                    ParserErrors.AddItem(lineNumber, $"Not valid start of result block expected '#R' but got '{line}'");

                if (line != null)
                    patient.Results.Add(ParseResultBlock(reader, line));
            }
            while ((line = ReadLine(reader)) != null && !line.StartsWith(@"#A/"));

            return patient;
        }

        Result ParseResultBlock(TextReader reader, string firstLine)
        {
            if ((new string[] { "#Ra", "#Rd", "#Rh", "#Rm", "#Rs" }).Contains(firstLine))
                return ParseNumericBlock(reader, firstLine);
            if (firstLine == "#Rb")
                return ParseTextResultBlock(reader);
            if (firstLine == "#Rc")
                return ParseResultTitleBlock(reader);

            ParserErrors.AddItem(lineNumber, $"Not valid start of result block '{firstLine}' must be #Ra or #Rd or #Rh or #Rm or #Rs or #Rb or #Rc");
            return null;
        }

        ResultTitle ParseResultTitleBlock(TextReader reader)
        {
            //(lijn 1:)#Rc positie 1-3:duidt begin aan van verslag)
            var title = new ResultTitle();

            //Lijn 2: identificatie van de analyse
            //Formaat:
            //ofwel: de Medidoc code van de analyse (8 karakters)
            //ofwel: een code door het labo zelf gevormd (8 karakters)
            //ofwel: een  !  gevolgd door de naam v.d. analyse (max. 56 karakters)
            var id = ReadLine(reader)?.Trim();
            if (id?.StartsWith("!") ?? false)
                title.Name = id?.TrimStart('!').CheckMaxSize(56, ParserErrors, lineNumber);
            else 
                title.Code = id?.CheckMaxSize(8, ParserErrors, lineNumber);

            //Lijn 3,4,... : commentaar (facultatief)
            //Een willekeurig aantal lijnen met op elke lijn:
            //  ofwel: max. 75 karakters vrije tekst (beperking niet meer van toepassing voor de pakketten van Corilus nv)
            //- ofwel: de code van een commentaarmodule (max 1 code per lijn)

            var sb = new StringBuilder();
            string line = null;
            while ((line = ReadLine(reader)) != null && !line.StartsWith(@"#R/"))
            {
                sb.AppendLine(line);
            }
            title.Comment = sb.Length > 0 ? sb.ToString() : null;

            return title;
        }

        NumericResult ParseNumericBlock(TextReader reader, string firstLine)
        {
            NumericResult result;

            switch (firstLine)
            {
                case "#Rd":
                    result = new DynamicResult() { TimeIndication = TimeIndication.Days };
                    break;
                case "#Rh":
                    result = new DynamicResult() { TimeIndication = TimeIndication.Hours };
                    break;
                case "#Rm":
                    result = new DynamicResult() { TimeIndication = TimeIndication.Minutes };
                    break;
                case "#Rs":
                    result = new DynamicResult() { TimeIndication = TimeIndication.Seconds };
                    break;
                default:
                    result = new NumericResult();
                    break;
            }

            //Lijn 1: aanduiding begin van een resultaat
            //formaat: #Ra 

            //Lijn 2: identificatie van de analyse
            //Formaat:
            //ofwel: de Medidoc code van de analyse (8 karakters)
            //ofwel: een code door het labo zelf gevormd (8 karakters)
            //ofwel: een  !  gevolgd door de naam v.d. analyse (max. 56 karakters)
            var id = ReadLine(reader)?.Trim();
            if (id?.StartsWith("!") ?? false)
                result.Name = id?.TrimStart('!').CheckMaxSize(56, ParserErrors, lineNumber);
            else
                result.Code = id?.CheckMaxSize(8, ParserErrors, lineNumber);

            //Lijn 3:	de uitslag zelf
            //formaat: 1 karakter, met name:
            //= indien exacte uitslag
            //< indien uitslag kleiner dan
            //> indien uitslag groter dan
            //gevolgd door een getal van maximaal 8 cijfers (decimaal of breuk)
            //Indien er (nog) geen uitslag beschikbaar is, dan wordt op deze lijn één van de volgende aanduidingen geplaatst:
            //ofwel:	=%%		=> betekent: uitslag volgt later
            //ofwel:	=%%%%	=> betekent: er is geen uitslag en mag ook niet meer verwacht worden.
            //ofwel:	=%% gevolgd door max. 75 karakters vrije tekst (beperking niet meer van toepassing voor de pakketten van Corilus nv) of de code van een standaard
            //commentaar (cfr. Appendix B) => betekent: er is geen uitslag, de tekst legt uit waarom.
            result.Value = ReadLine(reader)?.Trim();
            if (result.Value == null || !Regex.Match(result.Value, @"([><=][0-9./]{0,8})|(%{2})|(%{4})|(%{2}.{0,75})", RegexOptions.IgnoreCase).Success)
                ParserErrors.AddItem(lineNumber, $"Not a valid result value: '{result.Value}'");

            //Lijn 4:	de "Medidoc" eenheididentifikatie
            //formaat: 2 karakters
            result.Unit = ReadLine(reader)?.CheckMaxSize(2, ParserErrors, lineNumber).Trim();

            //Lijn 5:	aanduiding pathologisch/normaal (max. 6 karakters)
            var intensity = ReadLine(reader)?.CheckMaxSize(6, ParserErrors, lineNumber);
            switch (intensity?.Trim())
            {
                case "--":
                case "LL":
                case "1":
                    result.Intensity = ResultIntensity.GreatlyReduced;
                    break;
                case "-":
                case "L":
                case "2":
                    result.Intensity = ResultIntensity.Reduced;
                    break;
                case "=":
                case "N":
                case "3":
                case "":
                    result.Intensity = ResultIntensity.Normal;
                    break;
                case "+":
                case "H":
                case "4":
                    result.Intensity = ResultIntensity.Increased;
                    break;
                case "++":
                case "HH":
                case "5":
                    result.Intensity = ResultIntensity.GreatlyIncreased;
                    break;
                default:
                    result.Intensity = ResultIntensity.Normal;
                    break;
            }

            //Lijn 6,7,... : commentaar (facultatief)
            var sb = new StringBuilder();
            string line = null;
            while ((line = ReadLine(reader)) != null && !line.StartsWith(@"#R/"))
            {
                if (line.StartsWith(@"\"))
                    result.ReferenceValue = line.TrimStart('\\');
                else
                    sb.AppendLine(line);
            }
            result.Comment = sb.Length > 0 ? sb.ToString() : null;

            return result;
        }

        TextResult ParseTextResultBlock(TextReader reader)
        {
            //(lijn 1:)#Rb positie 1-3:duidt begin aan van verslag)
            var result = new TextResult();

            //(lijn 2:) evt identificatie van de analyse (positie 1-56)
            //formaat: '!'gevolgd door trefwoord
            var line = ReadLine(reader);
            if (line.StartsWith(@"!"))
            {
                result.Name = line?.Trim();
                line = ReadLine(reader);
            }

            //(lijn 3: vanaf hier begint het eigenlijke verslag)
            var sb = new StringBuilder();
            do
            {
                sb.AppendLine(line);
            }
            while ((line = ReadLine(reader)) != null && !line.StartsWith(@"#R/"));
            result.Text = sb.Length > 0 ? sb.ToString() : null;

            return result;
        }

        ExecutingDoctor ParseTextReportDoctorBlock(TextReader reader, string line)
        {
            var executingDoctor = new ExecutingDoctor();

            //(lijn 1:)RIZIV-nummer uitvoerend arts of paramedicus (positie 1-14)
            //formaat: C/CCCCC/CC/CCC
            if (line == null || !Regex.Match(line?.Trim(), @"\d/\d{5}/\d{2}/\d{3}", RegexOptions.IgnoreCase).Success)
                ParserErrors.AddItem(lineNumber, $"Not a valid rizivnumber: '{line}' of format C/CCCCC/CC/CCC");
            executingDoctor.RizivNr = line?.Replace("/", string.Empty).Trim();

            //(lijn 2:)Naam (positie 1-24) + Voornaam (positie 25-40)
            //uitvoerend arts of paramedicus
            var name = ReadLine(reader)?.CheckMaxSize(40, ParserErrors, lineNumber);
            executingDoctor.LastName = name?.Substring(0, name.Length > 24 ? 24 : name.Length).Trim();
            if (name?.Length > 24)
                executingDoctor.FirstName = name?.Substring(24).Trim();

            //(lijn 3:)Straat (positie 1-35) + nr (positie 36-45)
            //uitvoerend arts of paramedicus
            var address = ReadLine(reader)?.CheckMaxSize(45, ParserErrors, lineNumber);
            executingDoctor.Address.Street = address?.Substring(0, address.Length > 35 ? 35 : address.Length).Trim();
            if (address?.Length > 35)
                executingDoctor.Address.HouseNr = address?.Substring(35).Trim();

            //(lijn 4:)Postcode (positie 1-10) + Gemeente (positie 11-45)
            //uitvoerend arts of paramedicus
            address = ReadLine(reader)?.CheckMaxSize(45, ParserErrors, lineNumber);
            executingDoctor.Address.PostalCode = address?.Substring(0, address.Length > 10 ? 10 : address.Length).Trim();
            if (address?.Length > 10)
                executingDoctor.Address.Town = address?.Substring(10).Trim();

            //(lijn 5:)Telefoon- en faxnummer (vrije tekst) (positie 1-50)
            //uitvoerend arts of paramedicus
            executingDoctor.Phone = ReadLine(reader)?.CheckMaxSize(50, ParserErrors, lineNumber).Trim();

            //(lijn 6:)Boodschap (vrije tekst) (positie 1-50)
            executingDoctor.Message = ReadLine(reader)?.CheckMaxSize(50, ParserErrors, lineNumber).Trim();

            //(lijn 7:)Datum(+eventueel tijdstip) aanmaak diskette (positie 1-10)
            //formaat: JJJJMMDD(+evtHHMM)
            executingDoctor.Date = ReadLine(reader)?.ToNullableDatetime("yyyyMMddHHmm", "yyyyMMdd", "yyMMdd");


            executingDoctor.RequestingDoctor = new Doctor();
            //(lijn 8:)RIZIV-nummer aanvragende arts (positie 1-14)
            //formaat: C/CCCCC/CC/CCC
            executingDoctor.RequestingDoctor.RizivNr = ReadLine(reader)?.Replace("/", string.Empty).Trim();

            //(lijn 9:)Naam (positie 1-24) + Voornaam (positie 25-40)
            //aanvragende arts
            name = ReadLine(reader)?.CheckMaxSize(40, ParserErrors, lineNumber);
            executingDoctor.RequestingDoctor.LastName = name?.Substring(0, name.Length > 24 ? 24 : name.Length).Trim();
            if (name?.Length > 24)
                executingDoctor.RequestingDoctor.FirstName = name?.Substring(24).Trim();

            line = ReadLine(reader);
            do
            {
                if (line == null || !line.StartsWith("#A"))
                    ParserErrors.AddItem(lineNumber, $"Not valid start of patient block expected '#A' but got '{line}'");

                if (line != null)
                    executingDoctor.Patients.Add(ParsePatientBlock(reader, line, false));
            }
            while ((line = ReadLine(reader)) != null && !line.StartsWith(@"#/"));

            //line = sr.ReadLine();
            if (line == null || !line.StartsWith(@"#/"))
                ParserErrors.AddItem(lineNumber, $"Expected end of doctor blok '#/' but got '{line}'");

            return executingDoctor;
        }

        #endregion

        #region Methods

        string ReadLine(TextReader reader)
        {
            lineNumber++;
            return reader.ReadLine();
        }

        #endregion
    }
}
