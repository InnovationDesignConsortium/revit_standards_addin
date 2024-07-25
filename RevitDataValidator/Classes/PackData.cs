using System.Collections.ObjectModel;

namespace RevitDataValidator
{
    public class PackData
    {
        public string PackName { get; set; }

        public string LinkText
        {
            get
            {
                if (string.IsNullOrEmpty(LinkURL))
                    return null;
                return "URL";
            }
        }

        public string LinkURL { get; set; }

        public string PdfText
        {
            get
            {
                if (string.IsNullOrEmpty(PdfPath))
                    return null;
                return "PDF";
            }
        }

        public string PdfPath { get; set; }

        public ObservableCollection<IStateParameter> PackParameters { get; set; }
    }
}