﻿using System.ComponentModel.Composition;
using Microsoft.R.Core.AST;
using Microsoft.R.Core.Tokens;
using Microsoft.R.Editor.ContentType;
using Microsoft.R.Editor.Document;
using Microsoft.R.Editor.Document.Definitions;
using Microsoft.R.Editor.Tree.Definitions;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.BraceCompletion;
using Microsoft.VisualStudio.Text.Editor;
using Microsoft.VisualStudio.Utilities;

namespace Microsoft.R.Editor.Completion.AutoCompletion {

    [Export(typeof(IBraceCompletionContextProvider))]
    [ContentType(RContentTypeDefinition.ContentType)]
    [BracePair('{', '}')]
    [BracePair('[', ']')]
    [BracePair('(', ')')]
    [BracePair('\'', '\'')]
    [BracePair('\"', '\"')]
    internal sealed class BraceCompletionContextProvider : IBraceCompletionContextProvider {
        /// <summary>
        /// Creates an <see cref="IBraceCompletionContext"/> to handle 
        /// language-specific actions such as parsing and formatting.
        /// </summary>
        /// <remarks>
        /// Opening points within strings and comments are usually invalid points to start 
        /// an <see cref="IBraceCompletionSession"/> and will return false.
        /// </remarks>
        /// <param name="textView">View containing the <paramref name="openingPoint"/>.</param>
        /// <param name="openingPoint">Insertion point of the <paramref name="openingBrace"/>.</param>
        /// <param name="openingBrace">Opening brace that has been typed by the user.</param>
        /// <param name="closingBrace">Closing brace character</param>
        /// <param name="context">Brace completion context if created.</param>
        /// <returns>Returns true if the <paramref name="openingPoint"/> 
        /// was a valid point in the buffer to start a <see cref="IBraceCompletionSession"/>.
        /// </returns>
        public bool TryCreateContext(ITextView textView, SnapshotPoint openingPoint, char openingBrace, char closingBrace, out IBraceCompletionContext context) {
            IREditorDocument document = REditorDocument.TryFromTextBuffer(openingPoint.Snapshot.TextBuffer);
            if (document != null) {
                IEditorTree tree = document.EditorTree;
                tree.EnsureTreeReady();
                TokenNode node = tree.AstRoot.NodeFromPosition(openingPoint.Position) as TokenNode;
                if(node != null && node.Token.TokenType == RTokenType.String) {
                    context = null;
                    return false;
                }
            }
            context = new BraceCompletionContext();
            return true;
        }
    }
}