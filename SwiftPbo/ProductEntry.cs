#region

using System;
using System.Collections.Generic;

#endregion

namespace SwiftPbo
{
    [Serializable]
    public class ProductEntry
    {
        private List<string> _addtional = new List<string>();
        private string _name;
        private string _prefix;
        private string _productVersion;

        public ProductEntry()
        {
            _name = _prefix = _productVersion = "";
            Addtional = new List<string>();
        }

        public ProductEntry(string name, string prefix, string productVersion, List<string> addList = null)
        {
            Name = name;
            Prefix = prefix;
            ProductVersion = productVersion;
            if (addList != null)
                Addtional = addList;
        }

        public string Name
        {
            get => _name;
            set => _name = value;
        }

        public string Prefix
        {
            get => _prefix;
            set => _prefix = value;
        }

        public string ProductVersion
        {
            get => _productVersion;
            set => _productVersion = value;
        }

        public List<string> Addtional
        {
            get => _addtional;
            set => _addtional = value;
        }
    }
}