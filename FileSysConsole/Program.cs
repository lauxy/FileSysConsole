using System;
using System.Collections.Generic;
using System.Linq;

namespace FileSysConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            Execute exe = new Execute();
            exe.exeall();
            //Execute2 exe2 = new Execute2();
            //exe2.exeall();

            //exe.Install();
            //exe.Start();
            //exe.Creat(1, "new1.cpp");
            //Console.WriteLine(exe.sys_inode_tt.tt[0].di_table[0].name);
        }
    }
}
