using System.Collections.Generic;
namespace MediDocParser.Model
{
    public class Result
    {
        public string Id { get; set; }
    }

    public class TextResult : Result
    {      
        public string Text { get; set; }
    }

    public class ResultTitle : Result
    {
        public string Comment { get; set; }
    }

    public class NumericResult : Result
    {
        public string Value { get; set; }
        public string Unit { get; set; }
        public ResultIntensity Intensity { get; set; }
        public string Comment { get; set; }
        public string ReferenceValue { get; set; }
    }

    public class DynamicResult : NumericResult
    {
        public TimeIndication TimeIndication { get; set; }
    }
}
