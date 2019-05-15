using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Archivverwaltungssystem
{
    class DisplayDocument
    {
        public int DocumentId { get; set; }
        public string CategoryNumber { get; set; }
        public string DocumentName { get; set; }

        public override string ToString() => CategoryNumber + ": " + DocumentName;
    }
}
