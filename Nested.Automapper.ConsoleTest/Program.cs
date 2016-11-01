using Sigil;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nested.ConsoleTest
{
    
    class Program
    {
        static void Main(string[] args)
        {
            //List<Dictionary<string, object>> data = new List<Dictionary<string, object>>()
            //{
                
            //    new Dictionary<string, object>()
            //    {
            //        {"C", "data"},
            //        {"A.Data","data1"},
            //        {"A.Data1",2},
            //        {"B.Data","data3"},
            //        {"B.Data1",4},
            //        {"D.Data","data5"},
            //        {"D.Data1",6}
            //    },
            //    new Dictionary<string, object>()
            //    {
            //        {"C", "data"},
            //        {"A.Data","data1"},
            //        {"A.Data1",2},
            //        {"B.Data","data3"},
            //        {"B.Data1",4},
            //        {"D.Data","data7"},
            //        {"D.Data1",8}
            //    },
            //    new Dictionary<string, object>()
            //    {
            //        {"C", "data"},
            //        {"A.Data","data1"},
            //        {"A.Data1",2},
            //        {"B.Data","data9"},
            //        {"B.Data1",10},
            //        {"D.Data","data5"},
            //        {"D.Data1",6}
            //    },
            //    new Dictionary<string, object>()
            //    {
            //        {"C", "data"},
            //        {"A.Data","data1"},
            //        {"A.Data1",2},
            //        {"B.Data","data9"},
            //        {"B.Data1",10},
            //        {"D.Data","data7"},
            //        {"D.Data1",8}
            //    }
            //};

            List<Dictionary<string, object>> data = new List<Dictionary<string, object>>()
            {
                
                new Dictionary<string, object>()
                {
                    {"C", "data"},
                    {"A.Data","data1"},
                    {"A.Data1",2},
                    {"B.Data","data3"},
                    {"B.Data1",4},
                    {"D.Data","data5"},
                    //{"D.Data1",6}
                },
                new Dictionary<string, object>()
                {
                    {"C", "data"},
                    {"A.Data","data1"},
                    {"A.Data1",2},
                    {"B.Data","data3"},
                    {"B.Data1",4},
                    {"D.Data","data7"},
                    {"D.Data1",8}
                },
                new Dictionary<string, object>()
                {
                    {"C", "data"},
                    {"A.Data","data1"},
                    {"A.Data1",2},
                    {"B.Data","data9"},
                    {"B.Data1",10},
                    {"D.Data","data5"},
                    //{"D.Data1",null}
                },
                new Dictionary<string, object>()
                {
                    {"C", "data"},
                    {"A.Data","data1"},
                    {"A.Data1",2},
                    {"B.Data","data9"},
                    {"B.Data1",10},
                    {"D.Data","data7"},
                    {"D.Data1",8}
                }
            };
            //var meh = Automapper.GenerateMapper<TestType1>().CreateDelegate()(data);

            var result = Nested.Automapper.Map<TestType1>(data);
            
        }
    }

    public class TestType1
    {
        [Key]
        public string C { get; set; }

        
        public TestType5 A { get; set; }

        public List<TestType5> B { get; set; }
        public List<TestType6> D { get; set; }

    }

    //public class TestType2
    //{
    //    public TestType3 D { get; set; }

    //    public List<TestType4> C { get; set; }

    //}
    //public class TestType3
    //{
    //    public List<TestType2> B { get; set; }

    //}

    public class TestType4
    {
        [Key]
        public string Data { get; set; }

        public int Data1 { get; set; }
    }

    public class TestType5
    {
        [Key]
        public string Data { get; set; }

        public int Data1 { get; set; }
    }

    public class TestType6
    {
        [Key]
        public string Data { get; set; }

        public Nullable<int> Data1 { get; set; }
    }
}
