using System;
using System.Windows.Input;

namespace AutoNotifyGen
{
	class Program
	{
		static void Main(string[] args)
		{
			Console.WriteLine("Hello World!");
			var vm = new VM
			{
				Name = "Batata1234"
			};

			Console.WriteLine(vm.Name);
		}
	}

	partial class VM
	{
		[AutoNotify.AutoNotify]
		string _name;

		[AutoNotify.Command]
		public void MyCommandExecute()
        {
			
        }
	}

    public class Command : ICommand
    {
        public event EventHandler CanExecuteChanged;

        public bool CanExecute(object parameter)
        {
            throw new NotImplementedException();
        }

        public Command(Action action)
        {

        }

        public void Execute(object parameter)
        {
            throw new NotImplementedException();
        }
    }
}
