using MediDocParser.Model;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
                ParserErrors.AddItem(lineNumber, string.Format("Not valid start of lab block expected W,O,A,B,L followed by three digits but got '{0}'", line)); 
            lab.Id = line.Maybe(s => s.Trim());

            //Lijn 2 6: Identificatiegegevens van het labo (naam, adres, tel ...)
            //formaat: vrije tekst met maximaal 50 karakters per lijn .
            lab.Name = ReadLine(reader).Maybe(s => TrimToMaxSize(s, 50).Trim());
            lab.Address1 = ReadLine(reader).Maybe(s => TrimToMaxSize(s, 50).Trim());
            lab.Address2 = ReadLine(reader).Maybe(s => TrimToMaxSize(s, 50).Trim());
            lab.IdentificationData1 = ReadLine(reader).Maybe(s => TrimToMaxSize(s, 50).Trim());
            lab.IdentificationData2 = ReadLine(reader).Maybe(s => TrimToMaxSize(s, 50).Trim());

            //Lijn 7: datum (+ eventueel tijdstip) aanmaak
            //formaat: JJJJMMDD(+evtHHMM)
            lab.Date = ReadLine(reader).Maybe(s => s.ToNullableDatetime("yyyyMMddHHmm", "yyyyMMdd"));

            lab.RequestingDoctor = new Doctor();
            //Lijn 8: RIZIV nummer aanvragende arts
            //formaat: C/CCCCC/CC/CCC
            line = ReadLine(reader);
            if (line == null || !Regex.Match(line.Trim(), @"\d/\d{5}/\d{2}/\d{3}", RegexOptions.IgnoreCase).Success)
                ParserErrors.AddItem(lineNumber, string.Format("Not a valid rizivnumber: '{0}' of format C/CCCCC/CC/CCC", line));
            lab.RequestingDoctor.RizivNr = line.Maybe(s => s.Replace("/", string.Empty)).Maybe(s => s.Trim());

            //lijn 9: Naam (positie 1-24) + Voornaam (positie 25-40) aanvragende arts
            ReadLine(reader).Maybe(s =>
            {
                s = TrimToMaxSize(s, 40);
                lab.RequestingDoctor.LastName = s.Substring(0, s.Length > 24 ? 24 : s.Length).Maybe(ln => ln.Trim());
                if (s.Length > 24)
                    lab.RequestingDoctor.FirstName = s.Substring(24).Maybe(fn => fn.Trim());
                return s;
            });

            line = ReadLine(reader);
            do
            {
                if (line == null || !line.StartsWith("#A"))
                {
                    ParserErrors.AddItem(lineNumber, string.Format("Not valid start of patient block expected '#A' but got '{0}'", line));
                    break;
                }

                if (line != null)
                    lab.Patients.Add(ParsePatientBlock(reader, line, true));
            }
            while ((line = ReadLine(reader)) != null && !line.StartsWith(@"#/"));

            if (line == null || !line.StartsWith(@"#/"))
                ParserErrors.AddItem(lineNumber, string.Format("Expected end of lab blok '#/' but got '{0}'", line));

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
                        ParserErrors.AddItem(lineNumber, string.Format("Not a valid SSN: '{0}'", id));
                }
                else if (id.Length == 13)
                {
                    if (id == null || !Regex.Match(id.Trim(), @"\d{6}[XYZ][A-Z.]{6}", RegexOptions.IgnoreCase).Success)
                        ParserErrors.AddItem(lineNumber, string.Format("Not a valid Medidoc dossiernummer: '{0}'", id));
                }
                else
                {
                    ParserErrors.AddItem(lineNumber, string.Format("Invalid patient id '{0}'", firstLine));
                }

                patient.Id = id.Maybe(ln => ln.Trim());
            }

            //Lijn 2:	naam en voornaam van de patiënt
            ReadLine(reader).Maybe(s =>
            {
                s = TrimToMaxSize(s, 40);
                patient.LastName = s.Substring(0, s.Length > 24 ? 24 : s.Length).Maybe(ln => ln.Trim());
                if (s.Length > 24)
                    patient.FirstName = s.Substring(24).Maybe(fn => fn.Trim());
                return s;
            });

            //Lijn 3: geboortedatum patiënt
            //formaat: JJJJMMDD
            patient.BirthDate = ReadLine(reader).Maybe(s =>
                {
                    s = TrimToMaxSize(s, 8);
                    return s.ToNullableDatetime("yyyyMMdd");
                });

            //Lijn 4: geslacht patiënt
            //formaat: X, of Y, of Z
            ReadLine(reader).Maybe(s =>
            {
                s = TrimToMaxSize(s, 1);
                switch (s)
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
                return s;
            });

            //Lijn 5:	datum van de aanvraag
            //formaat: JJJJMMDD
            patient.RequestDate = ReadLine(reader).Maybe(s =>
            {
                s = TrimToMaxSize(s, 8);
                return s.ToNullableDatetime("yyyyMMdd");
            });

            //(Lijn 6:	referentienummer aanvraag 
            //formaat: max 14 karakters.
            patient.ReferenceNumber = ReadLine(reader).Maybe(s =>
            {
                return TrimToMaxSize(s, 14).Trim();
            });

            if (lab)
            {
                //Lijn 7:	protocol code
                //formaat: 1 karakter, zijnde: P indien partieel protocol; C indien volledig protocol; S indien aanvulling van een partieel; L indien het de laatste aanvulling is
                ReadLine(reader).Maybe(s =>
                {
                    s = TrimToMaxSize(s, 1);
                    switch (s)
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
                            ParserErrors.AddItem(lineNumber, string.Format("'{0}' is not a valid protocol code (only P,C,S and L) are allowed.", s));
                            break;
                    }
                    return s;
                });
            }
            else
            {
                //(lijn 7:)Episodenummer (positie 1-14); legt verband tussen meerdere onderzoeken
                //mag blanco gelaten worden
                patient.EpisodeNumber = ReadLine(reader).Maybe(s =>
                {
                    return TrimToMaxSize(s, 14).Trim();
                });
            }

            //(lijn 1-7 zijn obligaat, de volgende lijnen mogen weggelaten worden)
            var line = ReadLine(reader);
            if (line != null && !line.StartsWith(@"#R"))
            {
                //(lijn 8:)Straat (positie 1-24) + nr (positie 25-31)
                line.Maybe(s =>
                {
                    s = TrimToMaxSize(s, 31);
                    patient.Address.Street = s.Substring(0, s.Length > 35 ? 35 : s.Length).Maybe(str => str.Trim());
                    if (s.Length > 35)
                        patient.Address.HouseNr = s.Substring(35).Maybe(hn => hn.Trim());
                    return s;
                });

                line = ReadLine(reader);
                if (line != null && !line.StartsWith(@"#R"))
                {
                    //(lijn 9:)Postcode (positie 1-7)
                    patient.Address.PostalCode = line.Maybe(s => TrimToMaxSize(s, 7).Trim());

                    line = ReadLine(reader);
                    if (line != null && !line.StartsWith(@"#R"))
                    {
                        //(lijn 10:)Gemeente (positie 1-24)
                        patient.Address.Town = line.Maybe(s => TrimToMaxSize(s, 24).Trim());
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
                    ParserErrors.AddItem(lineNumber, string.Format("Not valid start of result block expected '#R' but got '{0}'", line));

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

            ParserErrors.AddItem(lineNumber, string.Format("Not valid start of result block '{0}' must be #Ra or #Rd or #Rh or #Rm or #Rs or #Rb or #Rc", firstLine));
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
            title.Id = ReadLine(reader).Maybe(s => s.StartsWith("!") ? TrimToMaxSize(s.TrimStart('!'), 56).Trim() : TrimToMaxSize(s, 8).Trim());

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
            result.Id = ReadLine(reader).Maybe(s => s.Trim()).Maybe(s => s.StartsWith("!") ? TrimToMaxSize(s.TrimStart('!'), 56) : TrimToMaxSize(s, 8));

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
            result.Value = ReadLine(reader).Maybe(s =>
                {
                    if (!Regex.Match(s.Trim(), @"([><=][0-9./]{0,8})|(%{2})|(%{4})|(%{2}.{0,75})", RegexOptions.IgnoreCase).Success)
                        ParserErrors.AddItem(lineNumber, string.Format("Not a valid result value: '{0}'", s));

                    return s.Trim();
                });

            //Lijn 4:	de "Medidoc" eenheididentifikatie
            //formaat: 2 karakters
            result.Unit = ReadLine(reader).Maybe(s => TrimToMaxSize(s, 2).Trim());

            //Lijn 5:	aanduiding pathologisch/normaal (max. 6 karakters)
            ReadLine(reader).Maybe(s =>
            {
                s = TrimToMaxSize(s, 6);
                switch (s)
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
                return s;
            });

            //Lijn 6,7,... : commentaar (facultatief)
            var sb = new StringBuilder();
            string line = null;
            while ((line = ReadLine(reader)) != null && !line.StartsWith(@"#R/"))
            {
                if (line.StartsWith(@"\"))
                    result.ReferenceValue = line;
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
                result.Id = line.Maybe(s => s.Trim());
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
            if (line == null || !Regex.Match(line.Trim(), @"\d/\d{5}/\d{2}/\d{3}", RegexOptions.IgnoreCase).Success)
                ParserErrors.AddItem(lineNumber, string.Format("Not a valid rizivnumber: '{0}' of format C/CCCCC/CC/CCC", line));
            executingDoctor.RizivNr = line.Maybe(s => s.Replace("/", string.Empty)).Maybe(s => s.Trim());

            //(lijn 2:)Naam (positie 1-24) + Voornaam (positie 25-40)
            //uitvoerend arts of paramedicus
            ReadLine(reader).Maybe(s =>
            {
                executingDoctor.LastName = s.Substring(0, s.Length > 24 ? 24 : s.Length).Maybe(ln => ln.Trim());
                if (s.Length > 24)
                    executingDoctor.FirstName = s.Substring(24).Maybe(fn => fn.Trim());
                return s;
            });

            //(lijn 3:)Straat (positie 1-35) + nr (positie 36-45)
            //uitvoerend arts of paramedicus
            ReadLine(reader).Maybe(s =>
            {
                executingDoctor.Address.Street = s.Substring(0, s.Length > 35 ? 35 : s.Length).Maybe(str => str.Trim()); //.Maybe(str => str.IfEmptyMakeNull())
                if (s.Length > 35)
                    executingDoctor.Address.HouseNr = s.Substring(35).Maybe(hn => hn.Trim());
                return s;
            });

            //(lijn 4:)Postcode (positie 1-10) + Gemeente (positie 11-45)
            //uitvoerend arts of paramedicus
            ReadLine(reader).Maybe(s =>
            {
                executingDoctor.Address.PostalCode = s.Substring(0, s.Length > 10 ? 10 : s.Length).Maybe(pc => pc.Trim());
                if (s.Length > 10)
                    executingDoctor.Address.Town = s.Substring(10).Maybe(t => t.Trim());
                return s;
            });

            //(lijn 5:)Telefoon- en faxnummer (vrije tekst) (positie 1-50)
            //uitvoerend arts of paramedicus
            executingDoctor.Phone = ReadLine(reader).Maybe(s => TrimToMaxSize(s, 50).Trim());

            //(lijn 6:)Boodschap (vrije tekst) (positie 1-50)
            executingDoctor.Message = ReadLine(reader).Maybe(s => TrimToMaxSize(s, 50).Trim());

            //(lijn 7:)Datum(+eventueel tijdstip) aanmaak diskette (positie 1-10)
            //formaat: JJJJMMDD(+evtHHMM)
            executingDoctor.Date = ReadLine(reader).Maybe(s => s.ToNullableDatetime("yyyyMMddHHmm", "yyyyMMdd"));


            executingDoctor.RequestingDoctor = new Doctor();
            //(lijn 8:)RIZIV-nummer aanvragende arts (positie 1-14)
            //formaat: C/CCCCC/CC/CCC
            executingDoctor.RequestingDoctor.RizivNr = ReadLine(reader).Maybe(s => s.Replace("/", string.Empty)).Maybe(s => s.Trim());

            //(lijn 9:)Naam (positie 1-24) + Voornaam (positie 25-40)
            //aanvragende arts
            ReadLine(reader).Maybe(s =>
            {
                executingDoctor.RequestingDoctor.LastName = s.Substring(0, s.Length > 24 ? 24 : s.Length).Maybe(ln => ln.Trim());
                if (s.Length > 24)
                    executingDoctor.RequestingDoctor.FirstName = s.Substring(24).Maybe(fn => fn.Trim());
                return s;
            });


            line = ReadLine(reader);
            do
            {
                if (line == null || !line.StartsWith("#A"))
                    ParserErrors.AddItem(lineNumber, string.Format("Not valid start of patient block expected '#A' but got '{0}'", line));

                if (line != null)
                    executingDoctor.Patients.Add(ParsePatientBlock(reader, line, false));
            }
            while ((line = ReadLine(reader)) != null && !line.StartsWith(@"#/"));

            //line = sr.ReadLine();
            if (line == null || !line.StartsWith(@"#/"))
                ParserErrors.AddItem(lineNumber, string.Format("Expected end of doctor blok '#/' but got '{0}'", line));

            return executingDoctor;
        }

        #endregion

        #region Methods

        string ReadLine(TextReader reader)
        {
            lineNumber++;
            return reader.ReadLine();
        }

        string TrimToMaxSize(string input, int max)
        {
            if ((input != null) && (input.Length > max))
                ParserErrors.AddItem(lineNumber, string.Format("Line exeeded max length of {0} characters: '{1}'", max, input));

            return input;
        }

        #endregion
    }
}
