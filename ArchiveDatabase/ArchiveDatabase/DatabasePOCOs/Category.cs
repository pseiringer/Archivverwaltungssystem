using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ArchiveDatabase.DatabasePOCOs
{
    public class Category
    {
        public int CategoryId { get; set; }

        [Index(IsUnique = true)]
        [Required]
        [StringLength(255)]
        public string CategoryNumber { get; set; }

        [Required]
        public string CategoryName { get; set; }

        public virtual Category SuperCategory { get; set; }

        public override string ToString()
        {
            return CategoryNumber + ": " + CategoryName;
        }
    }
}
