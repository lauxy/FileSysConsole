using FileSysTemp.FSBase;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Text;

namespace FileSysConsole
{
    public class DatabaseOp
    {
        private SQLiteConnection m_dbConnection = null; //数据库连接
        string dbPath = "InodeDb.sqlite";

        public DatabaseOp()
        {
            CreateNewDatabase();
            ConnectToDatabase();
            CreateTable();
        }

        /// <summary>
        /// 新建数据库
        /// </summary>
        /// <returns>返回数据库是否创建成功</returns>
        public bool CreateNewDatabase()
        {
            try
            {
                if (!File.Exists(dbPath))
                    SQLiteConnection.CreateFile(dbPath);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("新建数据库文件" + dbPath + "失败, Exception Infomation：" + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 创建数据库连接
        /// </summary>
        /// <returns>返回是否成功连接数据库</returns>
        public bool ConnectToDatabase()
        {
            try
            {
                m_dbConnection = new SQLiteConnection("Data Source=InodeDb.sqlite;Version=3;");
                m_dbConnection.Open();
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("打开数据库：InodeDb 的连接失败, Exception Infomation：" + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 在数据库中创建数据表Inode
        /// </summary>
        public void CreateTable()
        {
            if (m_dbConnection.State != System.Data.ConnectionState.Open)
            {
                bool issecceed = ConnectToDatabase();
            }
            string checkTableExist = "select count(*) from sqlite_master where type='table' and name='InodeTab'";
            SQLiteCommand checkcmd = new SQLiteCommand(checkTableExist, m_dbConnection);
            //判断数据表是否存在
            if (Convert.ToInt32(checkcmd.ExecuteScalar()) == 0)
            {
                string sql = "create table InodeTab (id integer primary key, name varchar(225), size real, t_create text, t_revise text, type text)";
                SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// 关闭数据库连接
        /// </summary>
        /// <returns></returns>
        public bool CloseDatabase()
        {
            if (m_dbConnection != null && m_dbConnection.State != System.Data.ConnectionState.Closed)
            {
                m_dbConnection.Close();
                m_dbConnection = null;
                return true;
            }
            return false;
        }

        /// <summary>
        /// 导入数据
        /// </summary>
        /// <param name="inodelist">批量数据</param>
        public void LoadDataToDb(List<DiskiNode> inodelist)
        {
            try
            {
                //采用事务操作可以实现批量数据的快速导入
                using (SQLiteTransaction dbTrans = m_dbConnection.BeginTransaction())
                {
                    using (SQLiteCommand cmd = m_dbConnection.CreateCommand())
                    {
                        foreach (DiskiNode inode in inodelist)
                        {
                            string sql =
                                string.Format("insert into InodeTab values ({0},'{1}',{2},'{3}','{4}','{5}')", inode.id, inode.name, inode.size, inode.t_create, inode.t_revise, inode.type);
                            cmd.CommandText = sql;
                            cmd.ExecuteNonQuery();
                        }
                    }
                    dbTrans.Commit();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("数据导入失败！Exception Information: " + ex.Message);
                return;
            }
        }


        /// <summary>
        /// 显示数据库中的数据，仅用于测试
        /// </summary>
        public void printHighscores()
        {
            string sql = "select * from InodeTab";
            SQLiteCommand command = new SQLiteCommand(sql, m_dbConnection);
            SQLiteDataReader reader = command.ExecuteReader();
            while (reader.Read())
                Console.WriteLine("Name: " + reader["name"]);
        }

        /// <summary>
        /// 执行用户输入的SQL语句
        /// </summary>
        /// <param name="sql"></param>
        /// <returns></returns>
        public bool ExecuteUserCmd(string sql)
        {
            try
            {
                SQLiteCommand cmd = m_dbConnection.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        Console.Write(reader[i].ToString() + " ");
                    }
                    Console.WriteLine();
                }
                return true;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 建立数据库索引，用于提高搜索效率
        /// </summary>
        public bool CreateIndex(string indexType)
        {
            string checkTableExist = "select count(*) from sqlite_master where type='table' and name='InodeTab'";
            SQLiteCommand checkcmd = new SQLiteCommand(checkTableExist, m_dbConnection);
            //判断数据表是否存在
            if (Convert.ToInt32(checkcmd.ExecuteScalar()) > 0)
            {
                ClearTableInDb();  //先确保清空数据表
            }
            else CreateTable();
            string sql = "create index if not exists index_{0} on InodeTab({0})";
            SQLiteCommand cmd = m_dbConnection.CreateCommand();
            string[] legelindex = { "id", "name", "size", "t_create", "t_revise", "type" };
            bool islegel = false;
            foreach (string item in legelindex)
            {
                if(item == indexType)
                {
                    try
                    {
                        sql = string.Format(sql, indexType);
                        cmd.CommandText = sql;
                        cmd.ExecuteNonQuery();
                        islegel = true;
                        Console.WriteLine("Successfully create index index_" + indexType);
                        break;
                    }
                    catch(Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                        return false;
                    }
                }
            }
            if (!islegel)
            {
                Console.WriteLine("Fail to create index " + indexType + ", please check if the attribute exist!");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 清楚数据库中的数据及建立的索引
        /// </summary>
        private bool ClearTableInDb()
        {
            string clrdat = "delete from InodeTab";
            SQLiteCommand cmd = m_dbConnection.CreateCommand();
            try
            {
                cmd.CommandText = clrdat;
                cmd.ExecuteNonQuery();
                return true;
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
                return false;
            }
        }

        /// <summary>
        /// 从数据库中搜索一个文件
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        public List<DiskiNode> SearchFileUsingDb(string filename)
        {
            string sql = "select * from InodeTab where name='" + filename + "'";
            List<DiskiNode> reslist = new List<DiskiNode>();
            try
            {
                SQLiteCommand cmd = m_dbConnection.CreateCommand();
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
                SQLiteDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    DiskiNode inode =
                        new DiskiNode(Convert.ToUInt32(reader[0]), reader[1].ToString(), Convert.ToUInt32(reader[2]), Convert.ToDateTime(reader[3]), Convert.ToDateTime(reader[4]), reader[5].ToString() == "FILE" ? ItemType.FILE : ItemType.FOLDER);
                    reslist.Add(inode);
                }
                return reslist;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return reslist;
            }
        }
    }
}
