using System;
using System.Collections.Generic;

namespace MediDocParser.Model
{
    public class ExecutingDoctor : Doctor
    {
        public ExecutingDoctor()
        {
            Patients = new List<Patient>();
            Address = new Address();
        }

        public Address Address { get; set; }
        public string Phone { get; set; }
        public string Message { get; set; }
        public DateTime? Date { get; set; }
        public Doctor RequestingDoctor { get; set; }

        public IList<Patient> Patients { get; set; }
    }
}
