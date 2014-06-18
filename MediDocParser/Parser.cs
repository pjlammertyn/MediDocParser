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
        public IList<ExecutingDoctor> ParseTextReport(string text)
        {
            if (text == null)
                throw new ArgumentNullException("text");

            using (var reader = new StringReader(text))
            {
                return ParseTextReport(reader);
            }
        }

        public IList<ExecutingDoctor> ParseTextReport(TextReader reader)
        {
            var executingDoctors = new List<ExecutingDoctor>();

            var line = reader.ReadLine();
            do
            {
                if (line == null || !Regex.Match(line.Trim(), @"\d/\d{5}/\d{2}/\d{3}", RegexOptions.IgnoreCase).Success)
                    throw new Exception(string.Format("Not valid start of doctor block expected 'd/ddddd/dd/ddd' but got '{0}'", line));

                executingDoctors.Add(ParseTextReportDoctorBlock(reader, line));
            }
            while ((line = reader.ReadLine()) != null);

            return executingDoctors;
        }

        public IList<Lab> ParseLabReport(string text)
        {
            if (text == null)
                throw new ArgumentNullException("text");

            using (var reader = new StringReader(text))
            {
                return ParseLabReport(reader);
            }
        }

        public IList<Lab> ParseLabReport(TextReader reader)
        {
            var labs = new List<Lab>();

            var line = reader.ReadLine();
            do
            {
                if (line == null || !Regex.Match(line.Trim(), @"[WOABL]\d{3}", RegexOptions.IgnoreCase).Success)
                    throw new Exception(string.Format("Not valid start of lab block expected W,O,A,B,L followed by three digits but got '{0}'", line));

                labs.Add(ParseLabBlock(reader, line));
            }
            while ((line = reader.ReadLine()) != null);

            return labs;
        }

        Lab ParseLabBlock(TextReader reader, string line)
        {
            var lab = new Lab();

            //Lijn 1: “Medidoc” identificatienummer van het laboratorium. 
            //formaat: 4 karakters en wordt als volgt gevormd: de eerste letter van de provincie (W,O,A,B,L) gevolgd door de eerste twee cijfers van de postkode, gevolgd door een volgnummer binnen de stad of gemeente. (vb. W841 voor een labo te Oostende)
            lab.Id = line.Maybe(s => s.Trim());

            //Lijn 2 6: Identificatiegegevens van het labo (naam, adres, tel ...)
            //formaat: vrije tekst met maximaal 50 karakters per lijn .
            lab.Name = reader.ReadLine().Maybe(s => s.TrimToMaxSize(50).Trim());
            lab.Address1 = reader.ReadLine().Maybe(s => s.TrimToMaxSize(50).Trim());
            lab.Address2 = reader.ReadLine().Maybe(s => s.TrimToMaxSize(50).Trim());
            lab.IdentificationData1 = reader.ReadLine().Maybe(s => s.TrimToMaxSize(50).Trim());
            lab.IdentificationData2 = reader.ReadLine().Maybe(s => s.TrimToMaxSize(50).Trim());

            //Lijn 7: datum (+ eventueel tijdstip) aanmaak
            //formaat: JJJJMMDD(+evtHHMM)
            lab.Date = reader.ReadLine().Maybe(s => s.ToNullableDatetime("yyyyMMddHHmm", "yyyyMMdd"));

            lab.RequestingDoctor = new Doctor();
            //Lijn 8: RIZIV nummer aanvragende arts
            //formaat: C/CCCCC/CC/CCC
            lab.RequestingDoctor.RizivNr = reader.ReadLine().Maybe(s => s.Replace("/", string.Empty)).Maybe(s => s.Trim());

            //lijn 9: Naam (positie 1-24) + Voornaam (positie 25-40) aanvragende arts
            reader.ReadLine().Maybe(s =>
            {
                lab.RequestingDoctor.LastName = s.Substring(0, s.Length > 24 ? 24 : s.Length).Maybe(ln => ln.Trim());
                if (s.Length > 24)
                    lab.RequestingDoctor.FirstName = s.Substring(24).Maybe(fn => fn.Trim());
                return s;
            });

            line = reader.ReadLine();
            do
            {
                if (line == null || !line.StartsWith("#A"))
                    throw new Exception(string.Format("Not valid start of patient block expected '#A' but got '{0}'", line));

                lab.Patients.Add(ParsePatientBlock(reader, line, true));
            }
            while (!(line = reader.ReadLine()).StartsWith(@"#/"));

            if (!line.StartsWith(@"#/"))
                throw new Exception(string.Format("Expected end of lab blok '#/' but got '{0}'", line));

            return lab;
        }

        Patient ParsePatientBlock(TextReader reader, string firstLine, bool lab)
        {
            var patient = new Patient();
            if (lab)
                patient.Address = null;

            //Lijn 1: aanduiding begin van een aanvraag formaat: #A (eventueel gevolgd het rijksregisternummer van de patient of bij gebrek hieraan het Medidoc dossiernummer van de patiënt   zie appendix A voor de vorming van het Medidoc dossiernummer)
            if (firstLine.Length > 2)
                patient.Id = firstLine.Substring(2).Maybe(ln => ln.Trim());

            //Lijn 2:	naam en voornaam van de patiënt
            reader.ReadLine().Maybe(s =>
            {
                patient.LastName = s.Substring(0, s.Length > 24 ? 24 : s.Length).Maybe(ln => ln.Trim());
                if (s.Length > 24)
                    patient.FirstName = s.Substring(24).Maybe(fn => fn.Trim());
                return s;
            });

            //Lijn 3: geboortedatum patiënt
            //formaat: JJJJMMDD
            patient.BirthDate = reader.ReadLine().Maybe(s => s.ToNullableDatetime("yyyyMMdd"));

            //Lijn 4: geslacht patiënt
            //formaat: X, of Y, of Z
            reader.ReadLine().Maybe(s =>
            {
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
            patient.RequestDate = reader.ReadLine().Maybe(s => s.ToNullableDatetime("yyyyMMdd"));

            //(Lijn 6:	referentienummer aanvraag 
            //formaat: max 14 karakters.
            patient.ReferenceNumber = reader.ReadLine().Maybe(s => s.Trim());

            if (lab)
            {
                //Lijn 7:	protocol code
                //formaat: 1 karakter, zijnde: P indien partieel protocol; C indien volledig protocol; S indien aanvulling van een partieel; L indien het de laatste aanvulling is
                reader.ReadLine().Maybe(s =>
                {
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
                            throw new Exception("'{0}' is not a valid protocol code (only P,C,S and L) are allowed.");
                    }
                    return s;
                });
            }
            else
            {
                //(lijn 7:)Episodenummer (positie 1-14); legt verband tussen meerdere onderzoeken
                //mag blanco gelaten worden
                patient.EpisodeNumber = reader.ReadLine().Maybe(s => s.Trim());
            }

            //(lijn 1-7 zijn obligaat, de volgende lijnen mogen weggelaten worden)
            var line = reader.ReadLine();
            if (!line.StartsWith(@"#R"))
            {
                //(lijn 8:)Straat (positie 1-24) + nr (positie 25-31)
                line.Maybe(s =>
                {
                    patient.Address.Street = s.Substring(0, s.Length > 35 ? 35 : s.Length).Maybe(str => str.Trim()); //.Maybe(str => str.IfEmptyMakeNull())
                    if (s.Length > 35)
                        patient.Address.HouseNr = s.Substring(35).Maybe(hn => hn.Trim());
                    return s;
                });

                line = reader.ReadLine();
                if (!line.StartsWith(@"#R"))
                {
                    //(lijn 9:)Postcode (positie 1-7)
                    patient.Address.PostalCode = line.Maybe(s => s.Trim());

                    line = reader.ReadLine();
                    if (!line.StartsWith(@"#R"))
                    {
                        //(lijn 10:)Gemeente (positie 1-24)
                        patient.Address.Town = line.Maybe(s => s.Trim());
                    }
                }
            }
            if (!line.StartsWith(@"#R"))
            {
                //(lijn 11 en volgende: in voorbereiding voor mut-gegevens, enz)
                while (!(line = reader.ReadLine()).StartsWith(@"#R"))
                { }
            }

            do
            {
                if (line == null || !line.StartsWith("#R"))
                    throw new Exception(string.Format("Not valid start of result block expected '#R' but got '{0}'", line));

                patient.Results.Add(ParseResultBlock(reader, line));
            }
            while (!(line = reader.ReadLine()).StartsWith(@"#A/"));

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

            throw new Exception(string.Format("Not valid start of result block '{0}'", firstLine));
        }

        ResultTitle ParseResultTitleBlock(TextReader reader)
        {
            //(lijn 1:)#Rb positie 1-3:duidt begin aan van verslag)
            var title = new ResultTitle();

            //Lijn 2: identificatie van de analyse
            //Formaat:
            //ofwel: de Medidoc code van de analyse (8 karakters)
            //ofwel: een code door het labo zelf gevormd (8 karakters)
            //ofwel: een  !  gevolgd door de naam v.d. analyse (max. 56 karakters)
            title.Id = reader.ReadLine().Maybe(s => s.Trim().TrimStart('!'));

            //Lijn 3,4,... : commentaar (facultatief)
            //Een willekeurig aantal lijnen met op elke lijn:
            //  ofwel: max. 75 karakters vrije tekst (beperking niet meer van toepassing voor de pakketten van Corilus nv)
            //- ofwel: de code van een commentaarmodule (max 1 code per lijn)

            var sb = new StringBuilder();
            string line = null;
            while (!(line = reader.ReadLine()).StartsWith(@"#R/"))
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
            result.Id = reader.ReadLine().Maybe(s => s.Trim().TrimStart('!'));

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
            result.Value = reader.ReadLine().Maybe(s => s.Trim());

            //Lijn 4:	de "Medidoc" eenheididentifikatie
            //formaat: 2 karakters
            result.Unit = reader.ReadLine().Maybe(s => s.Trim());

            //Lijn 5:	aanduiding pathologisch/normaal (max. 6 karakters)
            reader.ReadLine().Maybe(s =>
            {
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
            while (!(line = reader.ReadLine()).StartsWith(@"#R/"))
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
            var line = reader.ReadLine();
            if (line.StartsWith(@"!"))
            {
                result.Id = line.Maybe(s => s.Trim());
                line = reader.ReadLine();
            }

            //(lijn 3: vanaf hier begint het eigenlijke verslag)
            var sb = new StringBuilder();
            do
            {
                sb.AppendLine(line);
            }
            while (!(line = reader.ReadLine()).StartsWith(@"#R/"));
            result.Text = sb.Length > 0 ? sb.ToString() : null;

            return result;
        }

        //Patient ParseTextReportPatientBlock(TextReader reader, string firstLine)
        //{
        //    var patient = new Patient();

        //    ////(lijn 1:)#A(positie 1-2 : duidt begin aan van identificatie)
        //    if (firstLine.Length > 2)
        //        patient.Id = firstLine.Substring(2).Maybe(ln => ln.Trim());

        //    //(lijn 2:)Naam (positie 1-24) + Voornaam (positie 25-40)
        //    reader.ReadLine().Maybe(s =>
        //    {
        //        patient.LastName = s.Substring(0, s.Length > 24 ? 24 : s.Length).Maybe(ln => ln.Trim());
        //        if (s.Length > 24)
        //            patient.FirstName = s.Substring(24).Maybe(fn => fn.Trim());
        //        return s;
        //    });

        //    //(lijn 3:)Geboortedatum (positie 1-6)
        //    //formaat: JJJJMMDD
        //    patient.BirthDate = reader.ReadLine().Maybe(s => s.ToNullableDatetime("yyyyMMdd"));

        //    //(lijn 4:)Geslacht (positie 1)
        //    //formaat: X, of Y, of Z
        //    reader.ReadLine().Maybe(s =>
        //    {
        //        switch (s)
        //        {
        //            case "X":
        //                patient.Sex = Sex.female;
        //                break;
        //            case "Y":
        //                patient.Sex = Sex.male;
        //                break;
        //            default:
        //                patient.Sex = Sex.unknown;
        //                break;
        //        }
        //        return s;
        //    });

        //    //(lijn 5:)Datum van aanvraag van het onderzoek (positie 1-6)
        //    //formaat: JJJJMMDD
        //    patient.RequestDate = reader.ReadLine().Maybe(s => s.ToNullableDatetime("yyyyMMdd"));

        //    //(lijn 6:)Referentienummer aanvraag (positie 1-14)
        //    //mag blanco gelaten worden
        //    patient.ReferenceNumber = reader.ReadLine().Maybe(s => s.Trim());

        //    //(lijn 1-7 zijn obligaat, de volgende lijnen mogen weggelaten worden)
        //    var line = reader.ReadLine();
        //    if (!line.StartsWith(@"#Rb"))
        //    {
        //        //(lijn 8:)Straat (positie 1-24) + nr (positie 25-31)
        //        line.Maybe(s =>
        //        {
        //            patient.Address.Street = s.Substring(0, s.Length > 35 ? 35 : s.Length).Maybe(str => str.Trim()); //.Maybe(str => str.IfEmptyMakeNull())
        //            if (s.Length > 35)
        //                patient.Address.HouseNr = s.Substring(35).Maybe(hn => hn.Trim());
        //            return s;
        //        });

        //        line = reader.ReadLine();
        //        if (!line.StartsWith(@"#Rb"))
        //        {
        //            //(lijn 9:)Postcode (positie 1-7)
        //            patient.Address.PostalCode = line.Maybe(s => s.Trim());

        //            line = reader.ReadLine();
        //            if (!line.StartsWith(@"#Rb"))
        //            {
        //                //(lijn 10:)Gemeente (positie 1-24)
        //                patient.Address.Town = line.Maybe(s => s.Trim());
        //            }
        //        }
        //    }
        //    if (!line.StartsWith(@"#Rb"))
        //    {
        //        //(lijn 11 en volgende: in voorbereiding voor mut-gegevens, enz)
        //        while (!(line = reader.ReadLine()).StartsWith(@"#Rb"))
        //        { }
        //    }

        //    do
        //    {
        //        if (line == null || !line.StartsWith("#Rb"))
        //            throw new Exception(string.Format("Not valid start of report block expected '#Rb' but got '{0}'", line));

        //        patient.Reports.Add(ParseTextReportReportBlock(reader));
        //    }
        //    while (!(line = reader.ReadLine()).StartsWith(@"#A/"));

        //    return patient;
        //}

        ExecutingDoctor ParseTextReportDoctorBlock(TextReader reader, string line)
        {
            var executingDoctor = new ExecutingDoctor();

            //(lijn 1:)RIZIV-nummer uitvoerend arts of paramedicus (positie 1-14)
            //formaat: C/CCCCC/CC/CCC
            executingDoctor.RizivNr = line.Maybe(s => s.Replace("/", string.Empty)).Maybe(s => s.Trim());

            //(lijn 2:)Naam (positie 1-24) + Voornaam (positie 25-40)
            //uitvoerend arts of paramedicus
            reader.ReadLine().Maybe(s =>
            {
                executingDoctor.LastName = s.Substring(0, s.Length > 24 ? 24 : s.Length).Maybe(ln => ln.Trim());
                if (s.Length > 24)
                    executingDoctor.FirstName = s.Substring(24).Maybe(fn => fn.Trim());
                return s;
            });

            //(lijn 3:)Straat (positie 1-35) + nr (positie 36-45)
            //uitvoerend arts of paramedicus
            reader.ReadLine().Maybe(s =>
            {
                executingDoctor.Address.Street = s.Substring(0, s.Length > 35 ? 35 : s.Length).Maybe(str => str.Trim()); //.Maybe(str => str.IfEmptyMakeNull())
                if (s.Length > 35)
                    executingDoctor.Address.HouseNr = s.Substring(35).Maybe(hn => hn.Trim());
                return s;
            });

            //(lijn 4:)Postcode (positie 1-10) + Gemeente (positie 11-45)
            //uitvoerend arts of paramedicus
            reader.ReadLine().Maybe(s =>
            {
                executingDoctor.Address.PostalCode = s.Substring(0, s.Length > 10 ? 10 : s.Length).Maybe(pc => pc.Trim());
                if (s.Length > 10)
                    executingDoctor.Address.Town = s.Substring(10).Maybe(t => t.Trim());
                return s;
            });

            //(lijn 5:)Telefoon- en faxnummer (vrije tekst) (positie 1-50)
            //uitvoerend arts of paramedicus
            executingDoctor.Phone = reader.ReadLine().Maybe(s => s.TrimToMaxSize(50).Trim());

            //(lijn 6:)Boodschap (vrije tekst) (positie 1-50)
            executingDoctor.Message = reader.ReadLine().Maybe(s => s.TrimToMaxSize(50).Trim());

            //(lijn 7:)Datum(+eventueel tijdstip) aanmaak diskette (positie 1-10)
            //formaat: JJJJMMDD(+evtHHMM)
            executingDoctor.Date = reader.ReadLine().Maybe(s => s.ToNullableDatetime("yyyyMMddHHmm", "yyyyMMdd"));


            executingDoctor.RequestingDoctor = new Doctor();
            //(lijn 8:)RIZIV-nummer aanvragende arts (positie 1-14)
            //formaat: C/CCCCC/CC/CCC
            executingDoctor.RequestingDoctor.RizivNr = reader.ReadLine().Maybe(s => s.Replace("/", string.Empty)).Maybe(s => s.Trim());

            //(lijn 9:)Naam (positie 1-24) + Voornaam (positie 25-40)
            //aanvragende arts
            reader.ReadLine().Maybe(s =>
            {
                executingDoctor.RequestingDoctor.LastName = s.Substring(0, s.Length > 24 ? 24 : s.Length).Maybe(ln => ln.Trim());
                if (s.Length > 24)
                    executingDoctor.RequestingDoctor.FirstName = s.Substring(24).Maybe(fn => fn.Trim());
                return s;
            });


            line = reader.ReadLine();
            do
            {
                if (line == null || !line.StartsWith("#A"))
                    throw new Exception(string.Format("Not valid start of patient block expected '#A' but got '{0}'", line));

                executingDoctor.Patients.Add(ParsePatientBlock(reader, line, false));
            }
            while (!(line = reader.ReadLine()).StartsWith(@"#/"));

            //line = sr.ReadLine();
            if (!line.StartsWith(@"#/"))
                throw new Exception(string.Format("Expected end of doctor blok '#/' but got '{0}'", line));

            return executingDoctor;
        }
    }
}
