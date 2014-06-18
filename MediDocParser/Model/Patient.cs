using System;
using System.Collections.Generic;

namespace MediDocParser.Model
{
    public class Patient : Person
    {
        public Patient()
        {
            Address = new Address();
            Results = new List<Result>();
        }

        public string Id { get; set; }
        public DateTime? BirthDate { get; set; }
        public Sex Sex { get; set; }
        public DateTime? RequestDate { get; set; }
        public string ReferenceNumber { get; set; }
        public string EpisodeNumber { get; set; }
        public ProtocolCode ProtocolCode { get; set; }
        public Address Address { get; set; }

        public IList<Result> Results { get; set; }
    }
}
