using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using BoletoNetCore;

namespace teste
{
   public partial class Form1 : Form
   {
      public Form1()
      {
         InitializeComponent();
      }

      private void button1_Click(object sender, EventArgs e)
      {
         IBanco banco = Banco.Instancia(Bancos.Sicredi);
         ArquivoRetorno arquivoRetorno = new ArquivoRetorno(banco, TipoArquivo.CNAB400);

       
         Stream stream = new FileStream("D:\\marcelo\\Downloads\\65632916.CRT",FileMode.Open,FileAccess.Read);

         
         var b = arquivoRetorno.LerArquivoRetorno(stream);

         Console.ReadKey();
      }
   }
}
