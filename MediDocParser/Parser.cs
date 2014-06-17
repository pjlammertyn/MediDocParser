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
        public IList<ExecutingDoctor> ParseReport(string text)
        {
            if (text == null)
                throw new ArgumentNullException("text");

            using (var reader = new StringReader(text))
            {
                return ParseReport(reader);
            }
        }

        public IList<ExecutingDoctor> ParseReport(TextReader reader)
        {
            var executingDoctors = new List<ExecutingDoctor>();

            var line = reader.ReadLine();
            do
            {
                if (line == null || !Regex.Match(line.Trim(), @"\d/\d{5}/\d{2}/\d{3}", RegexOptions.IgnoreCase).Success)
                    throw new Exception(string.Format("Not valid start of doctor block expected 'd/ddddd/dd/ddd' but got '{0}'", line));

                executingDoctors.Add(ParseDoctorBlock(reader, line));
            }
            while ((line = reader.ReadLine()) != null);

            return executingDoctors;
        }


        Report ParseReportBlock(TextReader reader)
        {
            //(lijn 1:)#Rb positie 1-3:duidt begin aan van verslag)
            var report = new Report();
            var sb = new StringBuilder();

            //(lijn 2:) evt identificatie van de analyse (positie 1-56)
            //formaat: '!'gevolgd door trefwoord
            var line = reader.ReadLine();
            if (line.StartsWith(@"!"))
            {
                report.Id = line.Maybe(s => s.Trim());
                line = reader.ReadLine();
            }

            //(lijn 3: vanaf hier begint het eigenlijke verslag)
            do
            {
                sb.AppendLine(line);
            }
            while (!(line = reader.ReadLine()).StartsWith(@"#R/"));
            report.Text = sb.ToString();
            
            return report;
        }

        Patient ParsePatientBlock(TextReader reader)
        {
            //var line = sr.ReadLine();
            ////(lijn 1:)#A(positie 1-2 : duidt begin aan van identificatie)
            //if (line == null || !line.StartsWith("#A"))
            //    return false; //END-OF-FILE

            var patient = new Patient();

            //(lijn 2:)Naam (positie 1-24) + Voornaam (positie 25-40)
            reader.ReadLine().Maybe(s =>
            {
                patient.LastName = s.Substring(0, s.Length > 24 ? 24 : s.Length).Maybe(ln => ln.Trim());
                if (s.Length > 24)
                    patient.FirstName = s.Substring(24).Maybe(fn => fn.Trim());
                return s;
            });

            //(lijn 3:)Geboortedatum (positie 1-6)
            //formaat: JJJJMMDD
            patient.BirthDate = reader.ReadLine().Maybe(s => s.ToNullableDatetime("yyyyMMdd"));

            //(lijn 4:)Geslacht (positie 1)
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

            //(lijn 5:)Datum van aanvraag van het onderzoek (positie 1-6)
            //formaat: JJJJMMDD
            patient.RequestDate = reader.ReadLine().Maybe(s => s.ToNullableDatetime("yyyyMMdd"));

            //(lijn 6:)Referentienummer aanvraag (positie 1-14)
            //mag blanco gelaten worden
            patient.ReferenceNumber = reader.ReadLine().Maybe(s => s.Trim());

            //(lijn 7:)Episodenummer (positie 1-14); legt verband tussen meerdere onderzoeken
            //mag blanco gelaten worden
            patient.EpisodeNumber = reader.ReadLine().Maybe(s => s.Trim());

            //(lijn 1-7 zijn obligaat, de volgende lijnen mogen weggelaten worden)
            var line = reader.ReadLine();
            if (!line.StartsWith(@"#Rb"))
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
                if (!line.StartsWith(@"#Rb"))
                {
                    //(lijn 9:)Postcode (positie 1-7)
                    patient.Address.PostalCode = line.Maybe(s => s.Trim());

                    line = reader.ReadLine();
                    if (!line.StartsWith(@"#Rb"))
                    {
                        //(lijn 10:)Gemeente (positie 1-24)
                        patient.Address.Town = line.Maybe(s => s.Trim());
                    }
                }
            }
            if (!line.StartsWith(@"#Rb"))
            {
                //(lijn 11 en volgende: in voorbereiding voor mut-gegevens, enz)
                while (!(line = reader.ReadLine()).StartsWith(@"#Rb"))
                { }
            }

            do
            {
                if (line == null || !line.StartsWith("#Rb"))
                    throw new Exception(string.Format("Not valid start of report block expected '#Rb' but got '{0}'", line));

                patient.Reports.Add(ParseReportBlock(reader));
            }
            while (!(line = reader.ReadLine()).StartsWith(@"#A/"));

            return patient;
        }

        ExecutingDoctor ParseDoctorBlock(TextReader reader, string line)
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
            executingDoctor.Phone = reader.ReadLine().Maybe(s => s.Trim());

            //(lijn 6:)Boodschap (vrije tekst) (positie 1-50)
            executingDoctor.Message = reader.ReadLine().Maybe(s => s.Trim());

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

                executingDoctor.Patients.Add(ParsePatientBlock(reader));
            }
            while (!(line = reader.ReadLine()).StartsWith(@"#/"));

            //line = sr.ReadLine();
            if (!line.StartsWith(@"#/"))
                throw new Exception(string.Format("Expected end of doctor blok '#/' but got '{0}'", line));

            return executingDoctor;
        }
    }
}
