using SharpPcap;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Input;

namespace ManageDofus
{
    public partial class Form1 : Form
    {
        private const uint Restore = 9;
        List<DofusWindow> dofusWindow = new List<DofusWindow>();
        int actual = 0;
        string ip_server_dofus = "52.17.154.41";

        public Form1() {
            InitializeComponent();

            //var KeyboardHook = new Hook("Global Action Hook");
            //KeyboardHook.KeyDownEvent += Hook_KeyDown;

            Process[] process = Process.GetProcessesByName("dofus.dll");

            foreach (Process p in process) {
                var i = dofusWindow.Count;
                var b = (Button)Controls["character" + i];
                b.BackColor = System.Drawing.Color.SandyBrown;
                dofusWindow.Add(new DofusWindow {
                    _Process = p,
                    _Port = -1,
                    _IdCharacter = -1,
                    _Button = b
                });
            }

            process = null;

            Thread t = new Thread(() => {
                var devices = CaptureDeviceList.Instance;

                if (devices.Count < 1) {
                    //Console.WriteLine("No devices were found on this machine");
                    return;
                }
                var device = devices[0];
                device.OnPacketArrival += new PacketArrivalEventHandler(device_OnPacketArrival);

                device.Open();
                device.Capture();
                device.Close();
            });
            t.IsBackground = true;
            t.Start();

            Thread t2 = new Thread(() => {
                var down = false;
                while(true) {
                    Thread.Sleep(40);
                    if(Keyboard.IsKeyDown(Key.F3) && !down) {
                        down = true;
                        if (dofusWindow.Count > 1) {
                            if (++actual >= dofusWindow.Count) {
                                actual = 0;
                            }

                            ActivateWindow(dofusWindow[actual]._Process.MainWindowHandle);
                        }
                    } else if(Keyboard.IsKeyUp(Key.F3) && down) {
                        down = false;
                    }
                }
            });
            t2.SetApartmentState(ApartmentState.STA);
            t2.IsBackground = true;
            t2.Start();
        }

        /*private void Hook_KeyDown(KeyboardHookEventArgs e) {
            if(e.Key == Keys.F3) {
                if (dofusWindow.Count > 1) {
                    if (++actual >= dofusWindow.Count) {
                        actual = 0;
                    }

                    SetForegroundWindow(dofusWindow[actual]._Process.MainWindowHandle);
                }
            }
        }*/

        private void Form1_FormClosing(object sender, FormClosingEventArgs e) {
            
        }

        public static void ActivateWindow(IntPtr mainWindowHandle) {
            if (mainWindowHandle == GetForegroundWindow()) return;

            if (IsIconic(mainWindowHandle)) {
                ShowWindow(mainWindowHandle, Restore);
            }

            keybd_event(0, 0, 0, 0);

            SetForegroundWindow(mainWindowHandle);
        }

        private int SearchIndexDofusWindowByPort(ushort port) {
            int dw = -1;

            dw = dofusWindow.FindIndex(x => x._Port == port);
            if(dw == -1) {
                List<PRC> lprc = ProcManager.GetAllProcesses();
                PRC prc = lprc.Find(x => x.Port == port);

                dw = dofusWindow.FindIndex(x => x._Process.Id == prc.PID);
                if(dw == -1) {
                    dw = dofusWindow.Count;
                    var b = (Button)Controls["character" + dw];
                    b.BackColor = System.Drawing.Color.SandyBrown;
                    dofusWindow.Add(new DofusWindow {
                        _Process = Process.GetProcessById(prc.PID),
                        _Port = port,
                        _IdCharacter = -1,
                        _Button = b
                    });
                } else {
                    dofusWindow[dw]._Port = port;
                }

                lprc.Clear();
                lprc = null;
            }

            return dw;
        }

        private void device_OnPacketArrival(object sender, CaptureEventArgs e) {
            var packet = PacketDotNet.Packet.ParsePacket(e.Packet.LinkLayerType, e.Packet.Data);
            if (packet is PacketDotNet.EthernetPacket) {
                var ip = ((PacketDotNet.IPPacket)packet.Extract(typeof(PacketDotNet.IPPacket)));
                if (ip != null) {
                    string ipSrc = ip.SourceAddress.ToString();
                    string ipDst = ip.DestinationAddress.ToString();
                    if (ipSrc == ip_server_dofus || ipDst == ip_server_dofus) {
                        var tcp = (PacketDotNet.TcpPacket)packet.Extract(typeof(PacketDotNet.TcpPacket));
                        if (tcp != null) {
                            var data = Encoding.UTF8.GetString(tcp.PayloadData);
                            //Console.WriteLine(ipSrc + " >> " + ipDst + " : " + data);

                            int dw = -1;
                            bool dst = false;

                            if (tcp.DestinationPort == 443) {
                                dw = SearchIndexDofusWindowByPort(tcp.SourcePort);
                            } else {
                                dw = SearchIndexDofusWindowByPort(tcp.DestinationPort);
                                dst = true;
                            }

                            if (dw != -1) {
                                if (dofusWindow[dw]._IdCharacter == -1) {
                                    if (!dst) {
                                        if (data.Contains("AS")) {
                                            Match m = Regex.Match(data, @"AS(\d+)");
                                            dofusWindow[dw]._IdCharacter = Int32.Parse(m.Groups[1].Value);
                                            dofusWindow[dw]._Button.BackColor = System.Drawing.Color.MediumSeaGreen;
                                        } else if (data.Contains("eU1\n")) {
                                            dofusWindow[dw]._WaitEu1 = true;
                                        } else if (data.Contains("GA001")) {
                                            dofusWindow[dw]._WaitGa001 = true;
                                            Match m = Regex.Match(data, @"GA001(\S+)\n?");
                                            dofusWindow[dw]._PatternGa001 = m.Groups[1].Value;
                                        }
                                    } else {
                                        if (dofusWindow[dw]._WaitEu1 && data.Contains("ILF") && data.Contains("eUK")) {
                                            dofusWindow[dw]._WaitEu1 = false;
                                            Match m = Regex.Match(data, @"eUK(\d+)\|");
                                            dofusWindow[dw]._IdCharacter = Int32.Parse(m.Groups[1].Value);
                                            dofusWindow[dw]._Button.BackColor = System.Drawing.Color.MediumSeaGreen;
                                        } else if (dofusWindow[dw]._WaitGa001 && data.Contains("GA0;1;") && data.Contains(dofusWindow[dw]._PatternGa001)) {
                                            dofusWindow[dw]._WaitGa001 = false;
                                            Match m = Regex.Match(data, @"GA0;1;(\d+);");
                                            dofusWindow[dw]._IdCharacter = Int32.Parse(m.Groups[1].Value);
                                            dofusWindow[dw]._Button.BackColor = System.Drawing.Color.MediumSeaGreen;
                                        }
                                    }
                                } else {
                                    if(dst) {
                                        if(data.Contains("GTL|")) {
                                            Match m = Regex.Match(data, @"GTL\|([\d\|\-]+)");
                                            var gtl = m.Groups[1].Value.Split('|').Select(Int32.Parse).ToList();
                                            gtl.RemoveAll(x => x < 0);

                                            var tmp = new List<DofusWindow>(dofusWindow);
                                            dofusWindow.Clear();

                                            foreach (int v in gtl) {
                                                var dw2 = tmp.Find(x => x._IdCharacter == v);
                                                if(dw2 != null) {
                                                    dofusWindow.Add(dw2);
                                                }
                                            }

                                            tmp = null;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void button1_Click(object sender, EventArgs e) {
            ip_server_dofus = textBox1.Text;
        }

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern int ShowWindow(IntPtr hWnd, uint Msg);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
    }
}
