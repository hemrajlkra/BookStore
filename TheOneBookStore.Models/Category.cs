using System.ComponentModel.DataAnnotations;

namespace TheOneBookStore.Models
{
    public class Category
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        [Display(Name ="Display Order")]
        [Range(0,10,ErrorMessage ="Range can't be exceed by 10!!!")]
        public int DisplayOrder { get; set; }
        public DateTime CreatedDateTime { get; set; } = DateTime.Now;
    }
}
