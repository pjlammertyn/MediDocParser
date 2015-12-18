using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using MediDocParser.Model;

namespace MediDocParser
{
    public static class Composer
    {
        #region Compose Methods

        public static async Task ComposeTextReport(TextWriter writer, IEnumerable<ExecutingDoctor> executingDoctors)
        {
            var lineNumber = 0;
            foreach (var executingDoctor in executingDoctors)
                await ComposeTextReportDoctorBlock(writer, () => lineNumber, (ln) => lineNumber = ln, executingDoctor);
        }

        public static async Task ComposeLabReport(TextWriter writer, IEnumerable<Lab> labs)
        {
            var lineNumber = 0;
            foreach (var lab in labs)
                await ComposeLabBlock(writer, () => lineNumber, (ln) => lineNumber = ln, lab);
        }

        #endregion

        #region Private Compose Methods

        static async Task ComposeLabBlock(TextWriter writer, Func<int> lineNumberGetter, Action<int> lineNumberSetter, Lab lab)
        {
            var lineNumber = lineNumberGetter();

            //Lijn 1: “Medidoc” identificatienummer van het laboratorium. 
            //formaat: 4 karakters en wordt als volgt gevormd: de eerste letter van de provincie (W,O,A,B,L) gevolgd door de eerste twee cijfers van de postkode, gevolgd door een volgnummer binnen de stad of gemeente. (vb. W841 voor een labo te Oostende)
            lineNumber++;
            await writer.WriteLineAsync(lab.Id);

            //Lijn 2 6: Identificatiegegevens van het labo (naam, adres, tel ...)
            //formaat: vrije tekst met maximaal 50 karakters per lijn .
            lineNumber++;
            await writer.WriteLineAsync(lab.Name?.TrimToMaxSize(50));
            lineNumber++;
            await writer.WriteLineAsync(lab.Address1?.TrimToMaxSize(50));
            lineNumber++;
            await writer.WriteLineAsync(lab.Address2?.TrimToMaxSize(50));
            lineNumber++;
            await writer.WriteLineAsync(lab.IdentificationData1?.TrimToMaxSize(50));
            lineNumber++;
            await writer.WriteLineAsync(lab.IdentificationData2?.TrimToMaxSize(50));

            //Lijn 7: datum (+ eventueel tijdstip) aanmaak
            //formaat: JJJJMMDD(+evtHHMM)
            lineNumber++;
            await writer.WriteLineAsync(lab.Date?.IsMidnight() ?? false ?
                lab.Date?.ToString("yyyyMMdd") :
                lab.Date?.ToString("yyyyMMddHHmm"));

            //Lijn 8: RIZIV nummer aanvragende arts
            //formaat: C/CCCCC/CC/CCC
            lineNumber++;
            await writer.WriteLineAsync(Regex.Replace(lab.RequestingDoctor?.RizivNr ?? string.Empty, @"(\w{1})(\w{5})(\w{2})(\w{3})", @"$1/$2/$3/$4"));

            //lijn 9: Naam (positie 1-24) + Voornaam (positie 25-40) aanvragende arts
            lineNumber++;
            await writer.WriteLineAsync(string.Concat(lab.RequestingDoctor?.LastName?.TrimToMaxSize(24).PadRight(24) ?? string.Empty, lab.RequestingDoctor?.FirstName).TrimToMaxSize(40));

            foreach (var patient in lab.Patients)
                await ComposePatientBlock(writer, () => lineNumber, (ln) => lineNumber = ln, patient, true);

            lineNumber++;
            await writer.WriteLineAsync($"#/{lineNumber}");

            lineNumberSetter(lineNumber);
        }

        static async Task ComposePatientBlock(TextWriter writer, Func<int> lineNumberGetter, Action<int> lineNumberSetter, Patient patient, bool lab)
        {
            var lineNumber = lineNumberGetter();

            //Lijn 1: aanduiding begin van een aanvraag formaat: #A (eventueel gevolgd het rijksregisternummer van de patient of bij gebrek hieraan het Medidoc dossiernummer van de patiënt   zie appendix A voor de vorming van het Medidoc dossiernummer)
            lineNumber++;
            await writer.WriteLineAsync(string.Concat("#A", patient.Id));

            //Lijn 2:	naam en voornaam van de patiënt
            lineNumber++;
            await writer.WriteLineAsync(string.Concat(patient.LastName?.TrimToMaxSize(24).PadRight(24) ?? string.Empty, patient.FirstName).TrimToMaxSize(40));

            //Lijn 3: geboortedatum patiënt
            //formaat: JJJJMMDD
            lineNumber++;
            await writer.WriteLineAsync(patient.BirthDate?.ToString("yyyyMMdd"));

            //Lijn 4: geslacht patiënt
            //formaat: X, of Y, of Z
            lineNumber++;
            switch (patient.Sex)
            {
                case Sex.male:
                    await writer.WriteLineAsync("Y");
                    break;
                case Sex.female:
                    await writer.WriteLineAsync("X");
                    break;
                default:
                    await writer.WriteLineAsync("Z");
                    break;
            }

            //Lijn 5:	datum van de aanvraag
            //formaat: JJJJMMDD
            lineNumber++;
            await writer.WriteLineAsync(patient.RequestDate?.ToString("yyyyMMdd"));

            //(Lijn 6:	referentienummer aanvraag 
            //formaat: max 14 karakters.
            lineNumber++;
            await writer.WriteLineAsync(patient.ReferenceNumber?.TrimToMaxSize(14));

            if (lab)
            {
                //Lijn 7:	protocol code
                //formaat: 1 karakter, zijnde: P indien partieel protocol; C indien volledig protocol; S indien aanvulling van een partieel; L indien het de laatste aanvulling is
                lineNumber++;
                switch (patient.ProtocolCode)
                {
                    case ProtocolCode.Partial:
                        await writer.WriteLineAsync("P");
                        break;
                    case ProtocolCode.Full:
                        await writer.WriteLineAsync("C");
                        break;
                    case ProtocolCode.Adition:
                        await writer.WriteLineAsync("S");
                        break;
                    case ProtocolCode.LastAdition:
                        await writer.WriteLineAsync("L");
                        break;
                    default:
                        await writer.WriteLineAsync();
                        break;
                }
            }
            else
            {
                //(lijn 7:)Episodenummer (positie 1-14); legt verband tussen meerdere onderzoeken
                //mag blanco gelaten worden
                lineNumber++;
                await writer.WriteLineAsync(patient.EpisodeNumber?.TrimToMaxSize(14));
            }

            //(lijn 1-7 zijn obligaat, de volgende lijnen mogen weggelaten worden)
            //(lijn 8:)Straat (positie 1-24) + nr (positie 25-31)
            if (!string.IsNullOrEmpty(patient.Address?.Street))
            {
                lineNumber++;
                await writer.WriteLineAsync(string.Concat(patient.Address?.Street?.TrimToMaxSize(24).PadRight(24) ?? string.Empty, patient.Address?.HouseNr).TrimToMaxSize(31));
            }

            //(lijn 9:)Postcode (positie 1-7)
            if (!string.IsNullOrEmpty(patient.Address?.PostalCode))
            {
                lineNumber++;
                await writer.WriteLineAsync(patient.Address?.PostalCode?.TrimToMaxSize(7));
            }

            //(lijn 10:)Gemeente (positie 1-24)
            if (!string.IsNullOrEmpty(patient.Address?.Town))
            {
                lineNumber++;
                await writer.WriteLineAsync(patient.Address?.Town?.TrimToMaxSize(24));
            }

            foreach (var result in patient.Results)
               await  ComposeResultBlock(writer, () => lineNumber, (ln) => lineNumber = ln, result);

            lineNumber++;
            await writer.WriteLineAsync("#A/");

            lineNumberSetter(lineNumber);
        }

        static async Task ComposeResultBlock(TextWriter writer, Func<int> lineNumberGetter, Action<int> lineNumberSetter, Result result)
        {
            var lineNumber = lineNumberGetter();

            if (result is ResultTitle)
                await ComposeResultTitleBlock(writer, () => lineNumber, (ln) => lineNumber = ln, result as ResultTitle);
            else if (result is NumericResult)
                await ComposeNumericBlock(writer, () => lineNumber, (ln) => lineNumber = ln, result as NumericResult);
            else if (result is TextResult)
                await ComposeTextResultBlock(writer, () => lineNumber, (ln) => lineNumber = ln, result as TextResult);

            lineNumberSetter(lineNumber);
        }

        static async Task ComposeResultTitleBlock(TextWriter writer, Func<int> lineNumberGetter, Action<int> lineNumberSetter, ResultTitle result)
        {
            var lineNumber = lineNumberGetter();

            //(lijn 1:)#Rc positie 1-3:duidt begin aan van verslag)
            lineNumber++;
            await writer.WriteLineAsync("#Rc");

            //Lijn 2: identificatie van de analyse
            //Formaat:
            //ofwel: de Medidoc code van de analyse (8 karakters)
            //ofwel: een code door het labo zelf gevormd (8 karakters)
            //ofwel: een  !  gevolgd door de naam v.d. analyse (max. 56 karakters)
            lineNumber++;
            if (!string.IsNullOrEmpty(result.Code))
                await writer.WriteLineAsync(result.Code?.TrimToMaxSize(8));
            else
                await writer.WriteLineAsync(string.Concat("!", result.Name?.TrimToMaxSize(56)));

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
                        await writer.WriteLineAsync(line.TrimToMaxSize(75));
                    }
                }

            lineNumber++;
            await writer.WriteLineAsync("#R/");

            lineNumberSetter(lineNumber);
        }

        static async Task ComposeNumericBlock(TextWriter writer, Func<int> lineNumberGetter, Action<int> lineNumberSetter, NumericResult result)
        {
            var lineNumber = lineNumberGetter();

            //Lijn 1: aanduiding begin van een resultaat
            //formaat: #Ra 
            lineNumber++;
            if (result is DynamicResult)
            {
                switch ((result as DynamicResult).TimeIndication)
                {
                    case TimeIndication.Days:
                        await writer.WriteLineAsync("#Rd");
                        break;
                    case TimeIndication.Hours:
                        await writer.WriteLineAsync("#Rh");
                        break;
                    case TimeIndication.Minutes:
                        await writer.WriteLineAsync("#Rm");
                        break;
                    case TimeIndication.Seconds:
                        await writer.WriteLineAsync("#Rs");
                        break;
                }
            }
            else
                await writer.WriteLineAsync("#Ra");

            //Lijn 2: identificatie van de analyse
            //Formaat:
            //ofwel: de Medidoc code van de analyse (8 karakters)
            //ofwel: een code door het labo zelf gevormd (8 karakters)
            //ofwel: een  !  gevolgd door de naam v.d. analyse (max. 56 karakters)
            lineNumber++;
            if (!string.IsNullOrEmpty(result.Code))
                await writer.WriteLineAsync(result.Code?.TrimToMaxSize(8));
            else
                await writer.WriteLineAsync(string.Concat("!", result.Name?.TrimToMaxSize(56)));

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
            await writer.WriteLineAsync(result.Value);

            //Lijn 4:	de "Medidoc" eenheididentifikatie
            //formaat: 2 karakters
            lineNumber++;
            await writer.WriteLineAsync(result.Unit?.TrimToMaxSize(2));

            //Lijn 5:	aanduiding pathologisch/normaal (max. 6 karakters)
            lineNumber++;
            switch (result.Intensity)
            {
                case ResultIntensity.GreatlyReduced:
                    await writer.WriteLineAsync("--");
                    break;
                case ResultIntensity.Reduced:
                    await writer.WriteLineAsync("-");
                    break;
                case ResultIntensity.Normal:
                    await writer.WriteLineAsync("=");
                    break;
                case ResultIntensity.Increased:
                    await writer.WriteLineAsync("+");
                    break;
                case ResultIntensity.GreatlyIncreased:
                    await writer.WriteLineAsync("++");
                    break;
                default:
                    break;
            }

            //Lijn 6,7,... : commentaar (facultatief)
            if (result.ReferenceValue != null)
            {
                lineNumber++;
                await writer.WriteLineAsync(string.Concat(@"\", result.ReferenceValue));
            }

            if (result.Comment != null)
                using (var sr = new StringReader(result.Comment))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        lineNumber++;
                        await writer.WriteLineAsync(line/*.TrimToMaxSize(75)*/);
                    }
                }

            lineNumber++;
            await writer.WriteLineAsync("#R/");

            lineNumberSetter(lineNumber);
        }

        static async Task ComposeTextResultBlock(TextWriter writer, Func<int> lineNumberGetter, Action<int> lineNumberSetter, TextResult result)
        {
            var lineNumber = lineNumberGetter();

            //(lijn 1:)#Rb positie 1-3:duidt begin aan van verslag)
            lineNumber++;
            await writer.WriteLineAsync("#Rb");

            //(lijn 2:) evt identificatie van de analyse (positie 1-56)
            //formaat: '!'gevolgd door trefwoord
            lineNumber++;
            await writer.WriteLineAsync(string.Concat("!", result.Name?.TrimToMaxSize(56)));

            //(lijn 3: vanaf hier begint het eigenlijke verslag)
            using (var sr = new StringReader(result.Text))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    lineNumber++;
                    await writer.WriteLineAsync(line/*.TrimToMaxSize(75)*/);
                }
            }

            lineNumber++;
            await writer.WriteLineAsync("#R/");

            lineNumberSetter(lineNumber);
        }

        static async Task ComposeTextReportDoctorBlock(TextWriter writer, Func<int> lineNumberGetter, Action<int> lineNumberSetter, ExecutingDoctor executingDoctor)
        {
            var lineNumber = lineNumberGetter();

            //(lijn 1:)RIZIV-nummer uitvoerend arts of paramedicus (positie 1-14)
            //formaat: C/CCCCC/CC/CCC
            lineNumber++;
            await writer.WriteLineAsync(Regex.Replace(executingDoctor.RizivNr ?? string.Empty, @"(\w{1})(\w{5})(\w{2})(\w{3})", @"$1/$2/$3/$4"));

            //(lijn 2:)Naam (positie 1-24) + Voornaam (positie 25-40)
            //uitvoerend arts of paramedicus
            lineNumber++;
            await writer.WriteLineAsync(string.Concat(executingDoctor.LastName?.TrimToMaxSize(24).PadRight(24) ?? string.Empty, executingDoctor.FirstName).TrimToMaxSize(40));

            //(lijn 3:)Straat (positie 1-35) + nr (positie 36-45)
            //uitvoerend arts of paramedicus
            lineNumber++;
            await writer.WriteLineAsync(string.Concat(executingDoctor.Address?.Street?.TrimToMaxSize(35).PadRight(35) ?? string.Empty, executingDoctor.Address?.HouseNr).TrimToMaxSize(45));

            //(lijn 4:)Postcode (positie 1-10) + Gemeente (positie 11-45)
            //uitvoerend arts of paramedicus
            lineNumber++;
            await writer.WriteLineAsync(string.Concat(executingDoctor.Address?.PostalCode?.TrimToMaxSize(10).PadRight(10) ?? string.Empty, executingDoctor.Address?.HouseNr).TrimToMaxSize(45));

            //(lijn 5:)Telefoon- en faxnummer (vrije tekst) (positie 1-50)
            //uitvoerend arts of paramedicus
            lineNumber++;
            await writer.WriteLineAsync(executingDoctor.Phone?.TrimToMaxSize(50));

            //(lijn 6:)Boodschap (vrije tekst) (positie 1-50)
            lineNumber++;
            await writer.WriteLineAsync(executingDoctor.Message?.TrimToMaxSize(50));

            //(lijn 7:)Datum(+eventueel tijdstip) aanmaak diskette (positie 1-10)
            //formaat: JJJJMMDD(+evtHHMM)
            lineNumber++;
            await writer.WriteLineAsync(executingDoctor.Date?.IsMidnight() ?? false ?
                executingDoctor.Date?.ToString("yyyyMMdd") :
                executingDoctor.Date?.ToString("yyyyMMddHHmm"));

            //(lijn 8:)RIZIV-nummer aanvragende arts (positie 1-14)
            //formaat: C/CCCCC/CC/CCC
            lineNumber++;
            await writer.WriteLineAsync(Regex.Replace(executingDoctor.RequestingDoctor?.RizivNr ?? string.Empty, @"(\w{1})(\w{5})(\w{2})(\w{3})", @"$1/$2/$3/$4"));

            //(lijn 9:)Naam (positie 1-24) + Voornaam (positie 25-40)
            //aanvragende arts
            lineNumber++;
            await writer.WriteLineAsync(string.Concat(executingDoctor.RequestingDoctor?.LastName?.TrimToMaxSize(24).PadRight(24) ?? string.Empty, executingDoctor.RequestingDoctor?.FirstName).TrimToMaxSize(40));

            foreach (var patient in executingDoctor.Patients)
                await ComposePatientBlock(writer, () => lineNumber, (ln) => lineNumber = ln, patient, true);

            lineNumber++;
            await writer.WriteLineAsync($"#/{lineNumber}");

            lineNumberSetter(lineNumber);
        }

        #endregion
    }
}