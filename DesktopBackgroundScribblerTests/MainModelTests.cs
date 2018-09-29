using Microsoft.VisualStudio.TestTools.UnitTesting;
using DesktopBackgroundScribbler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Drawing;

namespace DesktopBackgroundScribbler.Tests
{
    [TestClass()]
    public class MainModelTests
    {
        [TestMethod()]
        public void ScribbleTest()
        {
            var mainModel = new MainModel();
            var texts = new[] { ".", "l", "鑑", "安以宇衣於加幾久計己左之寸世曽太知川天止奈仁奴祢乃波比不部保末美武女毛也以由江与良利留礼呂和為宇恵遠" };
            for (var i = 0; i < 50; i++)
            {
                foreach (var text in texts)
                {
                    mainModel.Scribble(text);
                    System.Threading.Thread.Sleep(100);
                }
            }
            Assert.IsTrue(true);
        }
    }
}