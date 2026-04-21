using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace RyC.Models
{
    public class RegisterRequestModel
    {
        public string username { get; set; }
        public string password { get; set; }
        public string type { get; set; }
        public string category { get; set; }
    }
}