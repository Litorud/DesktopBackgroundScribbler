using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DesktopBackgroundScribbler
{
    class History
    {
        List<string> texts = new List<string>();
        int index = 0;

        // 履歴が5個のとき:
        // [0][1][2][3][4] * 1回戻ると4、もう1回戻ると3、……
        // 履歴が1個のとき:
        // [0]             * 1回戻ると0、もう1回戻ることはできない。そこで進むと1。
        // 履歴が0個のとき:
        //                 * 戻ることも進むこともできない。

        internal void Push(string text)
        {
            texts.Add(text);
            index = texts.Count;
        }

        internal string ForwardHistory()
        {
            if (index == texts.Count)
            {
                // すでに最先端にいる。進むことはできない。
                return null;
            }

            index++;
            return index == texts.Count
                ? string.Empty // 最先端に進む。
                : texts[index];
        }

        internal string BackHistory()
        {
            return index == 0
                ? null
                : texts[--index];
        }
    }
}
