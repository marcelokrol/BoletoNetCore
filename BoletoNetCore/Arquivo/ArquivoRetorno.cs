using System;
using System.IO;
using System.Linq;

namespace BoletoNetCore
{
    public class ArquivoRetorno
    {
        public IBanco Banco { get; set; }
        public TipoArquivo TipoArquivo { get; set; }
        public Boletos Boletos { get; set; } = new Boletos();
        public DateTime? DataGeracao { get; set; }
        public int? NumeroSequencial { get; set; }

        private bool _ignorarCarteiraBoleto = false;

        #region Construtores

        public ArquivoRetorno(IBanco banco, TipoArquivo tipoArquivo, bool variasCarteiras = false)
        {
            Banco = banco;
            TipoArquivo = tipoArquivo;
            _ignorarCarteiraBoleto = variasCarteiras;
        }

        /// <summary>
        /// Neste construtor o boleto2net � respons�vel por atribuir o TipoArquivo e o Banco de acordo com o conte�do do arquivo de retorno.
        /// O pr�prio construtor chama o m�todo LerArquivoRetorno2 respons�vel por carregar/atribuir os boletos e demais informa��es do arquivo de retorno
        /// </summary>
        /// <param name="arquivo">Stream do arquivo de retorno</param>
        public ArquivoRetorno(Stream arquivo)
        {
            LerArquivoRetorno2(arquivo);
        }

        #endregion

        public Boletos LerArquivoRetorno(Stream arquivo)
        {
            Boletos.Clear();
            try
            {
                if (TipoArquivo == TipoArquivo.CNAB400 && Banco.IdsRetornoCnab400RegistroDetalhe.Count == 0)
                    throw new Exception("Banco " + Banco.Codigo.ToString() + " n�o implementou os Ids do Registro Retorno do CNAB400.");

                using (StreamReader arquivoRetorno = new StreamReader(arquivo, System.Text.Encoding.UTF8))
                {
                    while (!arquivoRetorno.EndOfStream)
                    {
                        var registro = arquivoRetorno.ReadLine();
                        if (TipoArquivo == TipoArquivo.CNAB240)
                        {
                            LerLinhaDoArquivoRetornoCNAB240(registro);
                        }
                        if (TipoArquivo == TipoArquivo.CNAB400)
                        {
                            LerLinhaDoArquivoRetornoCNAB400(registro);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao ler arquivo.", ex);
            }
            return Boletos;
        }

        private void LerArquivoRetorno2(Stream arquivo)
        {

            Boletos.Clear();
            try
            {
                using (StreamReader arquivoRetorno = new StreamReader(arquivo, System.Text.Encoding.UTF8))
                {
                    if (arquivoRetorno.EndOfStream)
                        return;

                    //busca o primeiro registro do arquivo
                    var registro = arquivoRetorno.ReadLine();

                    //atribui o tipo de acordo com o conte�do do arquivo
                    TipoArquivo = registro.Length == 240 ? TipoArquivo.CNAB240 : TipoArquivo.CNAB400;

                    //instacia o banco de acordo com o codigo/id do banco presente no arquivo de retorno
                    Banco = BoletoNetCore.Banco.Instancia(Utils.ToInt32(registro.Substring(TipoArquivo == TipoArquivo.CNAB240 ? 0 : 76, 3)));

                    if (TipoArquivo == TipoArquivo.CNAB400 && Banco.IdsRetornoCnab400RegistroDetalhe.Count == 0)
                        throw new Exception("Banco " + Banco.Codigo.ToString() + " n�o implementou os Ids do Registro Retorno do CNAB400.");

                    //define a posicao do reader para o in�cio
                    arquivoRetorno.DiscardBufferedData();
                    arquivoRetorno.BaseStream.Seek(0, SeekOrigin.Begin);

                    while (!arquivoRetorno.EndOfStream)
                    {
                        registro = arquivoRetorno.ReadLine();
                        if (TipoArquivo == TipoArquivo.CNAB240)
                        {
                            LerLinhaDoArquivoRetornoCNAB240(registro);
                        }else
                        if (TipoArquivo == TipoArquivo.CNAB400)
                        {
                            LerLinhaDoArquivoRetornoCNAB400(registro);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Erro ao ler arquivo.", ex);
            }

        }

        private void LerLinhaDoArquivoRetornoCNAB240(string registro)
        {
            IBancoCNAB240 b = (IBancoCNAB240)Banco;
            if (b == null) throw new Exception("Leitura CNAB240 n�o implementada para este banco.");

            var tipoRegistro = registro.Substring(7, 1);
            var tipoSegmento = registro.Substring(13, 1);

            if (tipoRegistro == "0")
            {
                //REGISTRO HEADER DO ARQUIVO RETORNO
                b.LerHeaderRetornoCNAB240(this, registro);
                return;
            }

            if (tipoRegistro == "3" & tipoSegmento == "T")
            {
                // Segmento T - Indica um novo boleto
                var boleto = new Boleto(this.Banco, _ignorarCarteiraBoleto);
                b.LerDetalheRetornoCNAB240SegmentoT(ref boleto, registro);
                Boletos.Add(boleto);
                return;
            }

            if (tipoRegistro == "3" & tipoSegmento == "U")
            {
                // Segmento U - Continua��o do segmento T anterior (localiza o �ltimo boleto da lista)
                Boleto boleto = Boletos.LastOrDefault();
                // Se n�o encontrou um boleto v�lido, ocorreu algum problema, pois deveria ter criado um novo objeto no registro que foi analisado anteriormente.
                if (boleto == null)
                    throw new Exception("Objeto boleto n�o identificado");
                b.LerDetalheRetornoCNAB240SegmentoU(ref boleto, registro);
                return;
            }

            if(tipoRegistro == "3" & tipoSegmento == "A")
            {
                // Segmento A - Indica um novo boleto
                var boleto = new Boleto(this.Banco, _ignorarCarteiraBoleto);
                b.LerDetalheRetornoCNAB240SegmentoA(ref boleto, registro);
                Boletos.Add(boleto);
                return;
            }
        }

        private void LerLinhaDoArquivoRetornoCNAB400(string registro)
        {
            IBancoCNAB400 b = (IBancoCNAB400)Banco;
            if (b == null) throw new Exception("Leitura CNAB400 n�o implementada para este banco.");

            // Identifica o tipo do registro (primeira posi��o da linha)
            var tipoRegistro = registro.Substring(0, 1);

            // Registro HEADER
            if (tipoRegistro == "0")
            {
                NumeroSequencial = int.Parse(registro.Substring(110, 7));
                b.LerHeaderRetornoCNAB400(registro);
                return;
            }

            // Registro TRAILER
            if (tipoRegistro == "9")
            {
                b.LerTrailerRetornoCNAB400(registro);
                return;
            }

            // Se o registro n�o estiver na lista a ser processada pelo banco selecionado, ignora o registro
            if (!Banco.IdsRetornoCnab400RegistroDetalhe.Contains(tipoRegistro))
                return;

            // O primeiro ID da lista, identifica um novo boleto.
            bool novoBoleto = false;
            if (tipoRegistro == Banco.IdsRetornoCnab400RegistroDetalhe.First())
                novoBoleto = true;

            // Se for um novo boleto, cria um novo objeto, caso contr�rio, seleciona o �ltimo boleto
            // Estamos considerando que, quando houver mais de um registro para o mesmo boleto, no arquivo retorno, os registros ser�o apresentados na sequencia.
            Boleto boleto = null;
            if (novoBoleto)
            {
                boleto = new Boleto(this.Banco, _ignorarCarteiraBoleto);
            }
            else
            {
                if (Boletos.Count > 0)
                    boleto = Boletos[Boletos.Count-1];

                // Se n�o encontrou um boleto v�lido, ocorreu algum problema, pois deveria ter criado um novo objeto no registro que foi analisado anteriormente.
                if (boleto == null)
                    throw new Exception("Objeto boleto n�o identificado");
            }


            // Identifica o tipo de registro que deve ser analisado pelo Banco.
            switch (tipoRegistro)
            {
                case "1":
                    b.LerDetalheRetornoCNAB400Segmento1(ref boleto, registro);
                    break;
                case "7":
                    b.LerDetalheRetornoCNAB400Segmento7(ref boleto, registro);
                    break;
                default:
                    break;
            }

            // Se for um novo boleto, adiciona na lista de boletos.
            if (novoBoleto)
            {
                Boletos.Add(boleto);
            }

        }
    }

}

