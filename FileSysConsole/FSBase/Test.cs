using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using static FileSysTemp.FSBase.Component;

namespace FileSysTemp.FSBase
{
    public class Test
    {
        public void Array1()
        {
            List<Data> alist = new List<Data>();
            DataInt a = new DataInt(57);
            DataIntArray b = new DataIntArray(1, 2);
            DataIntArrayArray c = new DataIntArrayArray(1, 2, 3);
            alist.Add(a);
            alist.Add(b);
            alist.Add(c);
            Console.WriteLine(alist[2].getItem(1, 2));
        }

        public void Write1()
        {
            FileStream fs2 = new FileStream("test.txt", FileMode.OpenOrCreate, FileAccess.ReadWrite);
            fs2.Seek(0, SeekOrigin.Begin);
            string str = "test for a test2";
            byte[] byteArray = Encoding.Default.GetBytes(str);
            fs2.Write(byteArray, 0, byteArray.Length);

            string str2 = "qaq";
            byte[] byteArray2 = Encoding.Default.GetBytes(str2);
            fs2.Seek(0, SeekOrigin.Begin);
            fs2.Write(byteArray2, 2, byteArray2.Length - 2);
            fs2.Close();
        }

        public void Dir1()
        {
            /*
            //List<DiskiNode> fanme = new List<DiskiNode>();
            DiskiNode root_inode;
            root_inode = new DiskiNode(0, "root", 0, 0);
            root_inode.next_addr = new List<uint>();
            root_inode.next_addr.Add(1);
            root_inode.next_addr.Add(2);
            root_inode.next_addr.Add(3);
            sys_inode_table.di_table.Clear();
            sys_inode_table.di_table.Add(root_inode);

            DiskiNode inode;
            inode = new DiskiNode(1, "note.cpp", 1, 0);
            inode.next_addr = new List<uint>();
            inode.next_addr.Add(4477);
            sys_inode_table.di_table.Add(inode);

            inode = new DiskiNode(2, "note2.txt", 2, 0);
            inode.next_addr = new List<uint>();
            inode.next_addr.Add(4577);
            inode.next_addr.Add(4578);
            sys_inode_table.di_table.Add(inode);

            inode = new DiskiNode(3, "newFolder", 0, 0);
            inode.next_addr = new List<uint>();
            sys_inode_table.di_table.Add(inode);
            //当前文件夹i节点信息都存在dn里了
            List<DiskiNode> dn = exe.TestShow(0);
            //输出root下的文件和文件夹的名字,类型,大小
            for (int i = 0; i < 4; i++)
            {
                Console.Write(dn[i].name);
                Console.Write("|");
                if (dn[i].block_num == 0)
                { Console.Write("文件夹"); Console.WriteLine(""); }
                else
                {
                    Console.Write("文件"); Console.Write("|"); Console.Write(dn[i].block_num); Console.WriteLine("KB");
                }
            }
            */
        }

        public void BlockLeader()
        {
            FileStream fs = new FileStream("filesystem", FileMode.OpenOrCreate, FileAccess.ReadWrite);
            BinaryFormatter binFormat = new BinaryFormatter();
            fs.Position = 4000 + 300 * 128 * SuperBlock.BLOCK_SIZE;
            BlockLeader bl = new BlockLeader();
            bl = (BlockLeader)binFormat.Deserialize(fs);
            Console.WriteLine(bl.block_addr[100]);
        }
    }
}
