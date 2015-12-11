using System;
using System.Collections.Generic;

namespace MediDocParser.Model
{
    public class Lab
    {
        public Lab()
        {
            Patients = new List<Patient>();

            ParserErrors = new Dictionary<int, IList<string>>();
        }

        public string Id { get; set; }
        public string Name { get; set; }
        public string Address1 { get; set; }
        public string Address2 { get; set; }
        public string IdentificationData1 { get; set; }
        public string IdentificationData2 { get; set; }
        public DateTime? Date { get; set; }
        public Doctor RequestingDoctor { get; set; }

        public IList<Patient> Patients { get; set; }

        public IDictionary<int, IList<string>> ParserErrors { get; set; }
    }
}
