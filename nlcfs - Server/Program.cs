using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace nlcfs___Server
{
  class Program
  {
    static void Main(string[] args)
    {
      var srv = new Server();
      srv.bDebugLog = true;
      srv.Start();
      Process.GetCurrentProcess().WaitForExit();
    }
  }
}
