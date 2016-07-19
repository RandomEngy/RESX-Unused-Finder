using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;

namespace ResxUnusedFinder
{
    public class ReferenceFormatViewModel : ObservableObject
    {
        private string value;
        public string Value
        {
            get { return this.value; }
            set { this.Set(ref this.value, value); }
        }
    }
}
