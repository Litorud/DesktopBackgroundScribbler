using Prism.Commands;
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

        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }

        public ICommand ForwardHistoryCommand { get; }
        public ICommand BackHistoryCommand { get; }

        public MainWindowModel()
        {
            ScribbleCommand = new DelegateCommand(Scribble);
            UndoCommand = new DelegateCommand(Undo);
            RedoCommand = new DelegateCommand(Redo);
            ForwardHistoryCommand = new DelegateCommand(ForwardHistory);
            BackHistoryCommand = new DelegateCommand(BackHistory);
        }

        private void Scribble()
        {
            if (string.IsNullOrWhiteSpace(Text))
            {
                Application.Current.Shutdown();
                return;
            }

            mainModel.Scribble(Text);
            Text = string.Empty;
        }

        private void Undo()
        {
            mainModel.Undo();
        }

        private void Redo()
        {
            mainModel.Redo();
        }

        private void ForwardHistory()
        {
            var text = mainModel.ForwardHistory();
            if (text != null)
            {
                Text = text;
            }
        }

        private void BackHistory()
        {
            var text = mainModel.BackHistory();
            if (text != null)
            {
                Text = text;
            }
        }

        internal void SaveHistory()
        {
            mainModel.SaveHistory();
        }
    }
}
