using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArchiveDatabase.DatabasePOCOs
{
    public class Document
    {
        public int DocumentId { get; set; }

        [StringLength(255)]
        [Required]
        public string DocumentName { get; set; }

        public string DocumentNotes { get; set; }

        public DateTime DocumentDate { get; set; }

        [Required]
        public virtual Category SuperCategory { get; set; }

        public override string ToString()
        {
            return DocumentName;
        }
    }
}
