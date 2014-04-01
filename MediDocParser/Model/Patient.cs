using System;
using System.Collections.Generic;

namespace MediDocParser.Model
{
    public class Patient : Person
    {
        public Patient()
        {
            Address = new Address();
            Reports = new List<Report>();
        }

        public DateTime? BirthDate { get; set; }
        public Sex Sex { get; set; }
        public DateTime? RequestDate { get; set; }
        public string ReferenceNumber { get; set; }
        public string EpisodeNumber { get; set; }
        public Address Address { get; set; }

        public IList<Report> Reports { get; set; }
    }
}
