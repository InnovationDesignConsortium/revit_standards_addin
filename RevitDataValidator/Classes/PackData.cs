using System.Collections.ObjectModel;

namespace RevitDataValidator
{
    public class PackData
    {
        private string _linkURL;
        private string _PdfPath;
        public string ParameterName { get; set; }

        public string LinkText
        {
            get
            {
                if (_linkURL == null || _linkURL == string.Empty)
                    return null;
                return "URL";
            }
        }

        public string LinkURL
        {
            get
            {
                return _linkURL;
            }
            set
            {
                _linkURL = value;
            }
        }

        public string PdfText
        {
            get
            {
                if (_PdfPath == null || _PdfPath == string.Empty)
                    return null;
                return "PDF";
            }
        }

        public string PdfPath
        {
            get
            {
                return _PdfPath;
            }
            set
            {
                _PdfPath = value;
            }
        }

        public ObservableCollection<IStateParameter> StateParametersList { get; set; }
    }
}