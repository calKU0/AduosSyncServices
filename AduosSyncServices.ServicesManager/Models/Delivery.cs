using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AduosSyncServices.ServicesManager.Models
{
    public class Delivery
    {
        public int Width { get; set; }
        public int Length { get; set; }
        public int Height { get; set; }
        public decimal Weight { get; set; }
        public string DeliveryName { get; set; }
    }
}