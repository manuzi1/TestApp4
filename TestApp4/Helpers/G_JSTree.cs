using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace TestApp4.Helpers
{
    public class G_JSTree
    {
        public G_JsTreeAttribute attr;
        public G_JSTree[] children;

        public string data { get; set; }
        public int IdServerUse { get; set; }
        public string state { get; set; }
        public string icons { get; set; }
    }

    public class G_JsTreeAttribute
    {
        public string id;
        public bool selected;
    }
}