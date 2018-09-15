using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace DesktopBackgroundScribbler
{
    class MainWindowModel : DependencyObject
    {
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            "Text",
            typeof(string),
            typeof(MainWindowModel),
            null);

        MainModel mainModel = new MainModel();

        public string Text
        {
            get { return (string)GetValue(TextProperty); }
            set { SetValue(TextProperty, value); }
        }

        public ICommand ScribbleCommand { get; }

        public MainWindowModel()
        {
            ScribbleCommand = new ScribbleCommandImpl(mainModel);
        }

        class ScribbleCommandImpl : ICommand
        {
            private MainModel mainModel;

            public event EventHandler CanExecuteChanged;

            public ScribbleCommandImpl(MainModel mainModel)
            {
                this.mainModel = mainModel;
            }

            public bool CanExecute(object parameter)
            {
                return true;
            }

            public void Execute(object parameter)
            {
                if (parameter is string text && !string.IsNullOrWhiteSpace(text))
                {
                    mainModel.Scribble(text);
                }
                else
                {
                    Application.Current.Shutdown();
                }
            }
        }
    }
}
