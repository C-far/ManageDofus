using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ManageDofus
{
    public class DofusWindow {
        public Process _Process { get; set; }
        public int _Port { get; set; }
        public int _IdCharacter { get; set; }
        public bool _WaitEu1 { get; set; }
        public bool _WaitGa001 { get; set; }
        public string _PatternGa001 { get; set; }
        public Button _Button { get; set; }
    }
}
