using Microsoft.VisualStudio.TestTools.UnitTesting;
using DesktopBackgroundScribbler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DesktopBackgroundScribbler.Tests
{
    [TestClass()]
    public class MainModelTests
    {
        [TestMethod()]
        public void Scribble2Test()
        {
            var mainModel = new MainModel();
            var texts = new[] { "l", "MM", "安以宇衣於加幾久計己左之寸世曽太知川天止奈仁奴祢乃波比不部保末美武女毛也以由江与良利留礼呂和為宇恵遠" };
            for (var i = 0; i < 50; i++)
            {
                foreach (var t in texts)
                {
                    mainModel.Scribble2(t);
                    System.Threading.Thread.Sleep(100);
                }
            }
            Assert.IsTrue(true);
        }

        [TestMethod()]
        public void ScribbleY座標Test()
        {
            var mainModel = new MainModel();
            var texts = new[] { "博麗霊夢" };
            for (var i = 0; i < 50; i++)
            {
                foreach (var t in texts)
                {
                    mainModel.Scribble2(t);
                    System.Threading.Thread.Sleep(100);
                }
            }
            Assert.IsTrue(true);
        }
    }
}