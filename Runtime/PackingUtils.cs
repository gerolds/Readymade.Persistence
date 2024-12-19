using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Readymade.Persistence {
    /// <summary>
    /// Utility functions for packing and unpacking.
    /// </summary>
    public static class PackingUtils {
        private static readonly StringBuilder s_Builder = new ();
        private static readonly Stack<string> s_Parts = new ();

        /// <summary>
        /// Constructs the scene hierarchy path to a given transform. Useful for debugging.
        /// </summary>
        /// <param name="start">The transform to generate a path for.</param>
        /// <returns>The constructed path.</returns>
        public static string GetPath ( this Transform start ) {
            if ( start == null ) {
                return string.Empty;
            }

            s_Builder.Clear ();
            s_Parts.Clear ();
            Transform t = start;
            int depth = 0;
            while ( t ) {
                s_Parts.Push ( t.name );
                t = t.parent;
                depth++;
                if ( depth > 16 ) {
                    return String.Empty;
                }
            }

            while ( s_Parts.TryPop ( out string part ) ) {
                s_Builder.Append ( '/' );
                s_Builder.Append ( part );
            }

            return s_Builder.ToString ();
        }
    }
}