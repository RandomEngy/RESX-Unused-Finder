using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ResxUnusedFinder
{
    public class StringResource
    {
        public string Key { get; set; }

        public string Value { get; set; }

        public string ResourceFile { get; set; }

        public bool IsSelected { get; set; }
    }
}
