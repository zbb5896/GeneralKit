using GeneralKit;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Data;

namespace NUnitGeneralKit
{
    public class UnitKit
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void VerifyUnit()
        {
            TestModel testModel = new TestModel();
            testModel.Id = 100;//����ͨ����
            //testModel.Name = "��";//ͨ����
            //testModel.Name = "����";//ͨ��
            //testModel.Name = "��as";//ͨ����
            //testModel.Name = "������";//ͨ��
            //testModel.Name = "����������";//ͨ����
            testModel.Old = 1;//ͨ��
            //testModel.Old = 12;//ͨ����
            testModel.Old1 = 10;//ͨ��
            //testModel.Old1 = 8;//ͨ����
            testModel.Old2 = 8;//ͨ��
            testModel.Old2 = 7;//ͨ����

            testModel.ModelValidation();
            Assert.IsTrue(true);
        }

        [Test]
        public void NullAssert()
        {
            //��������
            string test = null;
            //Ĭ��ֵ��NULL��Ϊtrue
            test.IsNull();
            test.NotNull();

            //ֵ����
            int test1 = 0;
            //Ĭ��ֵ��NULL��Ϊtrue
            test1.IsNull();
            test1.NotNull();

            Assert.IsTrue(true);
        }

        [Test]
        public void EnumRemark()
        {
            TestEnum test = TestEnum.None;
            TestEnum test1 = TestEnum.True;
            TestEnum test2 = TestEnum.False;

            //""
            test.Remark();
            //"��ȷ"
            test1.Remark();
            //"����"
            test2.Remark();

            Assert.IsTrue(true);
        }

        [Test]
        public void ExistCountAssert()
        {
            List<string> test = new List<string>();
            ICollection<string> test1 = new List<string>();
            IEnumerable<string> test2 = new List<string>();
            Dictionary<string, string> test3 = new Dictionary<string, string>();

            test.Exist();
            test.NotExist();
            test1.Exist();
            test1.NotExist();
            test2.Exist();
            test2.NotExist();
            test3.Exist();
            test3.NotExist();

            Assert.IsTrue(true);
        }

        [Test]
        public void DataRowSetValue()
        {
            DataTable dt = new DataTable();
            dt.Columns.Add(new DataColumn("Filed1", typeof(string)));
            dt.Columns.Add(new DataColumn("Filed2", typeof(decimal)));
            dt.Columns.Add(new DataColumn("Filed3", typeof(decimal)));

            DataRow dr = dt.NewRow();
            dr["Filed1"] = null;
            //dr["Filed2"] = null;//ֵ���͸�ֵnull�ᱨ��
            //dr.CellSetValue("Filed2", null);//�Զ���������

            dt.Rows.Add(dr);
        }
    }

    public class TestModel : ICheckVerify
    {
        public TestModel() { }

        private int? id;
        private string name;
        private long old1;
        private long old2;
        private string adress;
        private DateTime? time;

        [Rule("ID", AllowEmpty = false, Error = "{0}����Ϊ��")]
        public int? Id { get => id; set => id = value; }

        [Rule("����", MinLength = 2, MaxLength = 4, ExpType = ExpType.Chinese, Error = "{2}���ͱ���Ϊ����")]
        public string Name { get => name; set => name = value; }

        [Rule("����", Greater = 9, Error = "{0}���ܴ���9")]
        public int? Old { get; set; }

        [Rule("����", Less = 9, Error = "{0}����С��9")]
        public long Old1 { get => old1; set => old1 = value; }

        [Rule("����", Equal = 7, Error = "{0}���ܵ���7")]
        public long Old2 { get => old2; set => old2 = value; }

        [Rule("��ַ", MinLength = 1, MaxLength = 10, Error = "{2}���ȱ�����1��10λ")]
        public string Adress { get => adress; set => adress = value; }

        [Rule("ʱ��")]
        public DateTime? Time { get => time; set => time = value; }

    }

    public enum TestEnum
    {
        None,
        [Remark("��ȷ")]
        True,
        [Remark("����")]
        False
    }
}