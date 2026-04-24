using System.ComponentModel.DataAnnotations;
using SalesApp.Attributes;

namespace SalesApp.DTOs
{
    public class UpdateUserMatriculaRequest
    {
        public DateTime? EndDate { get; set; }
        
        public bool? IsActive { get; set; }
        
        public bool? IsOwner { get; set; }
    }
}
