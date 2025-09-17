using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommerceBatteryShop.Domain.Entities
{
    public class FavoriteList
    {
        public int Id { get; set; }
        public int? UserId { get; set; }
        public User? User { get; set; }     
        public string? AnonId { get; set; }   // <-- add this
        public DateTime CreatedAt { get; set; }

        public ICollection<FavoriteListItem> Items { get; set; } = new List<FavoriteListItem>();
    }
}
