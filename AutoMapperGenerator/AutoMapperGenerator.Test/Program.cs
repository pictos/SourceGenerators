using System;

namespace AutoMapperGenerator.Test
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            var pessoa = new Pessoa { Nome = "Pedro", Idade = 28 };

            var x = nameof(PessoaDto.PessoaNome);

           // var pessoa = MapperClass.Mapper(dto);

            var pessoaDto = AutoMapperGenerator.MapperClass.MapperToPessoaDto(pessoa);
            var funcionario = AutoMapperGenerator.MapperClass.MapperToFuncionario(pessoa);

            Console.WriteLine(pessoaDto);
            Console.WriteLine(funcionario);
        }
    }




    public class PessoaDto
    {
        public string PessoaNome { get; set; }

        public int Idade { get; set; }

        public string outroTeste;

        public override string ToString()
        {
            return PessoaNome + " " + Idade;
        }
    }

    [AutoMapperGenerator.MapperTo(typeof(PessoaDto))]
    [AutoMapperGenerator.MapperTo(typeof(Funcionario))]
    public class Pessoa
    {
        [AutoMapperGenerator.PropertyName(nameof(PessoaDto.PessoaNome), typeof(PessoaDto))]
        [AutoMapperGenerator.PropertyName(nameof(Funcionario.FuncionarioNome), typeof(Funcionario))]
        public string Nome { get; set; }

        public int Idade { get; set; }

        string teste;
    }

    public class Funcionario
    {
        public string FuncionarioNome { get; set; }
        public int Idade { get; set; }

        public override string ToString()
        {
            return FuncionarioNome + " " + Idade;
        }
    }
}
