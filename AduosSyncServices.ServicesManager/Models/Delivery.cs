using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AduosSyncServices.Contracts.Settings;

namespace AduosSyncServices.ServicesManager.Models
{
    public class Delivery
    {
        public DeliveryRuleType RuleType { get; set; } = DeliveryRuleType.Standard;
        public DeliveryHandlingTime HandlingTime { get; set; } = DeliveryHandlingTime.PT24H;
        public decimal? NetPriceThreshold { get; set; }
        public int Width { get; set; }
        public int Length { get; set; }
        public int Height { get; set; }
        public decimal Weight { get; set; }
        public string DeliveryName { get; set; }
    }
}