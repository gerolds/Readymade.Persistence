/* MIT License
 * Copyright 2023 Gerold Schneider
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the “Software”), to
 * deal in the Software without restriction, including without limitation the
 * rights to use, copy, modify, merge, publish, distribute, sublicense, and/or
 * sell copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL
 * THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER
 * DEALINGS IN THE SOFTWARE.
 */

using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Readymade.Persistence {
    /// <summary>
    /// Holds components that display information about a single in the <see cref="PackSystemPresenter"/> UI.
    /// </summary>
    public class PackSystemFileDisplay : MonoBehaviour {
        /// <summary>
        /// Display for the path to the file.
        /// </summary>
        public TMP_Text path;

        /// <summary>
        /// Display for the filename of the file.
        /// </summary>
        public TMP_Text filename;

        /// <summary>
        /// Display for the date the file was last written to.
        /// </summary>
        public TMP_Text date;

        /// <summary>
        /// Display for the time the file was last written to.
        /// </summary>
        public TMP_Text age;

        /// <summary>
        /// Display for the size of the file.
        /// </summary>
        public TMP_Text size;

        /// <summary>
        /// Display for the load button.
        /// </summary>
        public Button loadButton;

        /// <summary>
        /// A canvas group that can control interactivity of the load functionality of the display.
        /// </summary>
        public CanvasGroup loadGroup;

        /// <summary>
        /// A canvas group that can controls interactivity on protected files.
        /// </summary>
        public CanvasGroup protectedGroup;

        /// <summary>
        /// Display for the overwrite button.
        /// </summary>
        public Button overwriteButton;

        /// <summary>
        /// A canvas group that can control interactivity of the overwrite functionality of the display.
        /// </summary>
        public CanvasGroup overwriteGroup;

        /// <summary>
        /// Display for the delete button.
        /// </summary>
        public Button deleteButton;

        /// <summary>
        /// A canvas group that can control interactivity of the delete functionality of the display.
        /// </summary>
        public CanvasGroup deleteGroup;
    }
}