using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using MediDocParser.Model;

namespace MediDocParser
{
    public static class Composer
    {
        #region Compose Methods

        public static void ComposeTextReport(TextWriter writer, IEnumerable<ExecutingDoctor> executingDoctors)
        {
            var lineNumber = 0;
            foreach (var executingDoctor in executingDoctors)
                ComposeTextReportDoctorBlock(writer, ref lineNumber, executingDoctor);
        }

        public static void ComposeLabReport(TextWriter writer, IEnumerable<Lab> labs)
        {
            var lineNumber = 0;
            foreach (var lab in labs)
                ComposeLabBlock(writer, ref lineNumber, lab);
        }

        #endregion

        #region Private Compose Methods

        static void ComposeLabBlock(TextWriter writer, ref int lineNumber, Lab lab)
        {
            //Lijn 1: “Medidoc” identificatienummer van het laboratorium. 
            //formaat: 4 karakters en wordt als volgt gevormd: de eerste letter van de provincie (W,O,A,B,L) gevolgd door de eerste twee cijfers van de postkode, gevolgd door een volgnummer binnen de stad of gemeente. (vb. W841 voor een labo te Oostende)
            lineNumber++;
            writer.WriteLine(lab.Id);

            //Lijn 2 6: Identificatiegegevens van het labo (naam, adres, tel ...)
            //formaat: vrije tekst met maximaal 50 karakters per lijn .
            lineNumber++;
            writer.WriteLine(lab.Name?.TrimToMaxSize(50));
            lineNumber++;
            writer.WriteLine(lab.Address1?.TrimToMaxSize(50));
            lineNumber++;
            writer.WriteLine(lab.Address2?.TrimToMaxSize(50));
            lineNumber++;
            writer.WriteLine(lab.IdentificationData1?.TrimToMaxSize(50));
            lineNumber++;
            writer.WriteLine(lab.IdentificationData2?.TrimToMaxSize(50));

            //Lijn 7: datum (+ eventueel tijdstip) aanmaak
            //formaat: JJJJMMDD(+evtHHMM)
            lineNumber++;
            writer.WriteLine(lab.Date?.IsMidnight() ?? false ?
                lab.Date?.ToString("yyyyMMdd") :
                lab.Date?.ToString("yyyyMMddHHmm"));

            //Lijn 8: RIZIV nummer aanvragende arts
            //formaat: C/CCCCC/CC/CCC
            lineNumber++;
            writer.WriteLine(Regex.Replace(lab.RequestingDoctor?.RizivNr ?? string.Empty, @"(\w{1})(\w{5})(\w{2})(\w{3})", @"$1/$2/$3/$4"));

            //lijn 9: Naam (positie 1-24) + Voornaam (positie 25-40) aanvragende arts
            lineNumber++;
            writer.WriteLine(string.Concat(lab.RequestingDoctor?.LastName?.TrimToMaxSize(24).PadRight(24) ?? string.Empty, lab.RequestingDoctor?.FirstName).TrimToMaxSize(40));

            foreach (var patient in lab.Patients)
                ComposePatientBlock(writer, ref lineNumber, patient, true);

            lineNumber++;
            writer.WriteLine($"#/{lineNumber}");
        }

        static void ComposePatientBlock(TextWriter writer, ref int lineNumber, Patient patient, bool lab)
        {
            //Lijn 1: aanduiding begin van een aanvraag formaat: #A (eventueel gevolgd het rijksregisternummer van de patient of bij gebrek hieraan het Medidoc dossiernummer van de patiënt   zie appendix A voor de vorming van het Medidoc dossiernummer)
            lineNumber++;
            writer.WriteLine(string.Concat("#A", patient.Id));

            //Lijn 2:	naam en voornaam van de patiënt
            lineNumber++;
            writer.WriteLine(string.Concat(patient.LastName?.TrimToMaxSize(24).PadRight(24) ?? string.Empty, patient.FirstName).TrimToMaxSize(40));

            //Lijn 3: geboortedatum patiënt
            //formaat: JJJJMMDD
            lineNumber++;
            writer.WriteLine(patient.BirthDate?.ToString("yyyyMMdd"));

            //Lijn 4: geslacht patiënt
            //formaat: X, of Y, of Z
            lineNumber++;
            switch (patient.Sex)
            {
                case Sex.male:
                    writer.WriteLine("Y");
                    break;
                case Sex.female:
                    writer.WriteLine("X");
                    break;
                default:
                    writer.WriteLine("Z");
                    break;
            }

            //Lijn 5:	datum van de aanvraag
            //formaat: JJJJMMDD
            lineNumber++;
            writer.WriteLine(patient.RequestDate?.ToString("yyyyMMdd"));

            //(Lijn 6:	referentienummer aanvraag 
            //formaat: max 14 karakters.
            lineNumber++;
            writer.WriteLine(patient.ReferenceNumber?.TrimToMaxSize(14));

            if (lab)
            {
                //Lijn 7:	protocol code
                //formaat: 1 karakter, zijnde: P indien partieel protocol; C indien volledig protocol; S indien aanvulling van een partieel; L indien het de laatste aanvulling is
                lineNumber++;
                switch (patient.ProtocolCode)
                {
                    case ProtocolCode.Partial:
                        writer.WriteLine("P");
                        break;
                    case ProtocolCode.Full:
                        writer.WriteLine("C");
                        break;
                    case ProtocolCode.Adition:
                        writer.WriteLine("S");
                        break;
                    case ProtocolCode.LastAdition:
                        writer.WriteLine("L");
                        break;
                    default:
                        writer.WriteLine();
                        break;
                }
            }
            else
            {
                //(lijn 7:)Episodenummer (positie 1-14); legt verband tussen meerdere onderzoeken
                //mag blanco gelaten worden
                lineNumber++;
                writer.WriteLine(patient.EpisodeNumber?.TrimToMaxSize(14));
            }

            //(lijn 1-7 zijn obligaat, de volgende lijnen mogen weggelaten worden)
            //(lijn 8:)Straat (positie 1-24) + nr (positie 25-31)
            if (!string.IsNullOrEmpty(patient.Address?.Street))
            {
                lineNumber++;
                writer.WriteLine(string.Concat(patient.Address?.Street?.TrimToMaxSize(24).PadRight(24) ?? string.Empty, patient.Address?.HouseNr).TrimToMaxSize(31));
            }

            //(lijn 9:)Postcode (positie 1-7)
            if (!string.IsNullOrEmpty(patient.Address?.PostalCode))
            {
                lineNumber++;
                writer.WriteLine(patient.Address?.PostalCode?.TrimToMaxSize(7));
            }

            //(lijn 10:)Gemeente (positie 1-24)
            if (!string.IsNullOrEmpty(patient.Address?.Town))
            {
                lineNumber++;
                writer.WriteLine(patient.Address?.Town?.TrimToMaxSize(24));
            }

            foreach (var result in patient.Results)
                ComposeResultBlock(writer, ref lineNumber, result);

            lineNumber++;
            writer.WriteLine("#A/");
        }

        static void ComposeResultBlock(TextWriter writer, ref int lineNumber, Result result)
        {
            if (result is ResultTitle)
                ComposeResultTitleBlock(writer, ref lineNumber, result as ResultTitle);
            else if (result is NumericResult)
                ComposeNumericBlock(writer, ref lineNumber, result as NumericResult);
            else if (result is TextResult)
                ComposeTextResultBlock(writer, ref lineNumber, result as TextResult);
        }

        static void ComposeResultTitleBlock(TextWriter writer, ref int lineNumber, ResultTitle result)
        {
            //(lijn 1:)#Rc positie 1-3:duidt begin aan van verslag)
            lineNumber++;
            writer.WriteLine("#Rc");

            //Lijn 2: identificatie van de analyse
            //Formaat:
            //ofwel: de Medidoc code van de analyse (8 karakters)
            //ofwel: een code door het labo zelf gevormd (8 karakters)
            //ofwel: een  !  gevolgd door de naam v.d. analyse (max. 56 karakters)
            lineNumber++;
            if (!string.IsNullOrEmpty(result.Code))
                writer.WriteLine(result.Code?.TrimToMaxSize(8));
            else
                writer.WriteLine(string.Concat("!", result.Name?.TrimToMaxSize(56)));

            //Lijn 3,4,... : commentaar (facultatief)
            //Een willekeurig aantal lijnen met op elke lijn:
            //  ofwel: max. 75 karakters vrije tekst (beperking niet meer van toepassing voor de pakketten van Corilus nv)
            //- ofwel: de code van een commentaarmodule (max 1 code per lijn)
            if (result.Comment != null)
                using (var sr = new StringReader(result.Comment))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        lineNumber++;
                        writer.WriteLine(line.TrimToMaxSize(75));
                    }
                }

            lineNumber++;
            writer.WriteLine("#R/");
        }

        static void ComposeNumericBlock(TextWriter writer, ref int lineNumber, NumericResult result)
        {
            //Lijn 1: aanduiding begin van een resultaat
            //formaat: #Ra 
            lineNumber++;
            if (result is DynamicResult)
            {
                switch ((result as DynamicResult).TimeIndication)
                {
                    case TimeIndication.Days:
                        writer.WriteLine("#Rd");
                        break;
                    case TimeIndication.Hours:
                        writer.WriteLine("#Rh");
                        break;
                    case TimeIndication.Minutes:
                        writer.WriteLine("#Rm");
                        break;
                    case TimeIndication.Seconds:
                        writer.WriteLine("#Rs");
                        break;
                }
            }
            else
                writer.WriteLine("#Ra");

            //Lijn 2: identificatie van de analyse
            //Formaat:
            //ofwel: de Medidoc code van de analyse (8 karakters)
            //ofwel: een code door het labo zelf gevormd (8 karakters)
            //ofwel: een  !  gevolgd door de naam v.d. analyse (max. 56 karakters)
            lineNumber++;
            if (!string.IsNullOrEmpty(result.Code))
                writer.WriteLine(result.Code?.TrimToMaxSize(8));
            else
                writer.WriteLine(string.Concat("!", result.Name?.TrimToMaxSize(56)));

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
            lineNumber++;
            writer.WriteLine(result.Value);

            //Lijn 4:	de "Medidoc" eenheididentifikatie
            //formaat: 2 karakters
            lineNumber++;
            writer.WriteLine(result.Unit?.TrimToMaxSize(2));

            //Lijn 5:	aanduiding pathologisch/normaal (max. 6 karakters)
            lineNumber++;
            switch (result.Intensity)
            {
                case ResultIntensity.GreatlyReduced:
                    writer.WriteLine("--");
                    break;
                case ResultIntensity.Reduced:
                    writer.WriteLine("-");
                    break;
                case ResultIntensity.Normal:
                    writer.WriteLine("=");
                    break;
                case ResultIntensity.Increased:
                    writer.WriteLine("+");
                    break;
                case ResultIntensity.GreatlyIncreased:
                    writer.WriteLine("++");
                    break;
                default:
                    break;
            }

            //Lijn 6,7,... : commentaar (facultatief)
            if (result.ReferenceValue != null)
            {
                lineNumber++;
                writer.WriteLine(string.Concat(@"\", result.ReferenceValue));
            }

            if (result.Comment != null)
                using (var sr = new StringReader(result.Comment))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        lineNumber++;
                        writer.WriteLine(line/*.TrimToMaxSize(75)*/);
                    }
                }

            lineNumber++;
            writer.WriteLine("#R/");
        }

        static void ComposeTextResultBlock(TextWriter writer, ref int lineNumber, TextResult result)
        {
            //(lijn 1:)#Rb positie 1-3:duidt begin aan van verslag)
            lineNumber++;
            writer.WriteLine("#Rb");

            //(lijn 2:) evt identificatie van de analyse (positie 1-56)
            //formaat: '!'gevolgd door trefwoord
            lineNumber++;
            writer.WriteLine(string.Concat("!", result.Name?.TrimToMaxSize(56)));

            //(lijn 3: vanaf hier begint het eigenlijke verslag)
            using (var sr = new StringReader(result.Text))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    lineNumber++;
                    writer.WriteLine(line/*.TrimToMaxSize(75)*/);
                }
            }

            lineNumber++;
            writer.WriteLine("#R/");
        }

        static void ComposeTextReportDoctorBlock(TextWriter writer, ref int lineNumber, ExecutingDoctor executingDoctor)
        {
            //(lijn 1:)RIZIV-nummer uitvoerend arts of paramedicus (positie 1-14)
            //formaat: C/CCCCC/CC/CCC
            lineNumber++;
            writer.WriteLine(Regex.Replace(executingDoctor.RizivNr ?? string.Empty, @"(\w{1})(\w{5})(\w{2})(\w{3})", @"$1/$2/$3/$4"));

            //(lijn 2:)Naam (positie 1-24) + Voornaam (positie 25-40)
            //uitvoerend arts of paramedicus
            lineNumber++;
            writer.WriteLine(string.Concat(executingDoctor.LastName?.TrimToMaxSize(24).PadRight(24) ?? string.Empty, executingDoctor.FirstName).TrimToMaxSize(40));

            //(lijn 3:)Straat (positie 1-35) + nr (positie 36-45)
            //uitvoerend arts of paramedicus
            lineNumber++;
            writer.WriteLine(string.Concat(executingDoctor.Address?.Street?.TrimToMaxSize(35).PadRight(35) ?? string.Empty, executingDoctor.Address?.HouseNr).TrimToMaxSize(45));

            //(lijn 4:)Postcode (positie 1-10) + Gemeente (positie 11-45)
            //uitvoerend arts of paramedicus
            lineNumber++;
            writer.WriteLine(string.Concat(executingDoctor.Address?.PostalCode?.TrimToMaxSize(10).PadRight(10) ?? string.Empty, executingDoctor.Address?.HouseNr).TrimToMaxSize(45));

            //(lijn 5:)Telefoon- en faxnummer (vrije tekst) (positie 1-50)
            //uitvoerend arts of paramedicus
            lineNumber++;
            writer.WriteLine(executingDoctor.Phone?.TrimToMaxSize(50));

            //(lijn 6:)Boodschap (vrije tekst) (positie 1-50)
            lineNumber++;
            writer.WriteLine(executingDoctor.Message?.TrimToMaxSize(50));

            //(lijn 7:)Datum(+eventueel tijdstip) aanmaak diskette (positie 1-10)
            //formaat: JJJJMMDD(+evtHHMM)
            lineNumber++;
            writer.WriteLine(executingDoctor.Date?.IsMidnight() ?? false ?
                executingDoctor.Date?.ToString("yyyyMMdd") :
                executingDoctor.Date?.ToString("yyyyMMddHHmm"));

            //(lijn 8:)RIZIV-nummer aanvragende arts (positie 1-14)
            //formaat: C/CCCCC/CC/CCC
            lineNumber++;
            writer.WriteLine(Regex.Replace(executingDoctor.RequestingDoctor?.RizivNr ?? string.Empty, @"(\w{1})(\w{5})(\w{2})(\w{3})", @"$1/$2/$3/$4"));

            //(lijn 9:)Naam (positie 1-24) + Voornaam (positie 25-40)
            //aanvragende arts
            lineNumber++;
            writer.WriteLine(string.Concat(executingDoctor.RequestingDoctor?.LastName?.TrimToMaxSize(24).PadRight(24) ?? string.Empty, executingDoctor.RequestingDoctor?.FirstName).TrimToMaxSize(40));

            foreach (var patient in executingDoctor.Patients)
                ComposePatientBlock(writer, ref lineNumber, patient, true);

            lineNumber++;
            writer.WriteLine($"#/{lineNumber}");
        }

        #endregion
    }
}