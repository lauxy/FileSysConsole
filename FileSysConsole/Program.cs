using System;
using System.Collections.Generic;
using System.Linq;

namespace FileSysConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            //Execute exe = new Execute();
            //exe.exeall();
            //Execute2 exe2 = new Execute2();
            //exe2.exeall();
            int[] ss = { 1, 2, 3, 4, 5 };
            IEnumerable<int> k = from c in ss
                     where c == 7
                     select c;
            Console.WriteLine(k.Count());


            //exe.Install();
            //exe.Start();
            //exe.Creat(1, "new1.cpp");
            //Console.WriteLine(exe.sys_inode_tt.tt[0].di_table[0].name);
        }
    }
}
