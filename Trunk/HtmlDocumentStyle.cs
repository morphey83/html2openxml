﻿/* Copyright (C) Olivier Nizet http://html2openxml.codeplex.com - All Rights Reserved
 * 
 * This source is subject to the Microsoft Permissive License.
 * Please see the License.txt file for more information.
 * All other rights reserved.
 * 
 * THIS CODE AND INFORMATION ARE PROVIDED "AS IS" WITHOUT WARRANTY OF ANY 
 * KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A
 * PARTICULAR PURPOSE.
 */
using System;
using System.Collections.Generic;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace NotesFor.HtmlToOpenXml
{
	/// <summary>
	/// Defines the styles to apply on OpenXml elements.
	/// </summary>
	public sealed class HtmlDocumentStyle
	{
        internal enum KnownStyles { Hyperlink, Caption }

		/// <summary>
		/// Occurs when a Style is missing in the MainDocumentPart but will be used during the conversion process.
		/// </summary>
		public event EventHandler<StyleEventArgs> StyleMissing;

		private RunStyleCollection runStyle;
		private TableStyleCollection tableStyle;
		private ParagraphStyleCollection paraStyle;
        private NumberingListStyleCollection listStyle;
		private OpenXmlDocumentStyleCollection knownStyles;
		private MainDocumentPart mainPart;


		internal HtmlDocumentStyle(MainDocumentPart mainPart)
		{
			PrepareStyles(mainPart);
			tableStyle = new TableStyleCollection(this);
			runStyle = new RunStyleCollection(this);
			paraStyle = new ParagraphStyleCollection(this);
            this.QuoteCharacters = QuoteChars.IE;
			this.mainPart = mainPart;
		}

		//____________________________________________________________________
		//

		#region PrepareStyles

		/// <summary>
		/// Preload the styles in the document to match localized style name.
		/// </summary>
		internal void PrepareStyles(MainDocumentPart mainPart)
		{
			knownStyles = new OpenXmlDocumentStyleCollection();
			if (mainPart.StyleDefinitionsPart == null) return;

			Styles styles = mainPart.StyleDefinitionsPart.Styles;

			foreach (var s in styles.Elements<Style>())
			{
				StyleName n = s.StyleName;
				if (n != null)
				{
					String name = n.Val.Value;
					if (name != s.StyleId) knownStyles[name] = s;
				}

				knownStyles.Add(s.StyleId, s);
			}
		}

		#endregion

		#region GetStyle

		/// <summary>
		/// Helper method to obtain the StyleId of a named style (invariant or localized name).
		/// </summary>
		/// <param name="name">The name of the style to look for.</param>
		/// <param name="styleType">True to obtain the character version of the given style.</param>
		/// <param name="ignoreCase">Indicate whether the search should be performed with the case-sensitive flag or not.</param>
		/// <returns>If not found, returns the given name argument.</returns>
		public String GetStyle(string name, StyleValues styleType = StyleValues.Paragraph, bool ignoreCase = false)
		{
			Style style;

			// OpenXml is case-sensitive but CSS is not.
			// We will try to find the styles another time with case-insensitive:
			if (ignoreCase)
			{
				if (!knownStyles.TryGetValueIgnoreCase(name, styleType, out style))
				{
					if (StyleMissing != null)
					{
						StyleMissing(this, new StyleEventArgs(name, mainPart, styleType));
						if (knownStyles.TryGetValueIgnoreCase(name, styleType, out style))
							return style.StyleId;
					}
					return null; // null means we ignore this style (css class)
				}

				return style.StyleId;
			}
			else
			{
				if (!knownStyles.TryGetValue(name, out style))
				{
					if (StyleMissing != null) StyleMissing(this, new StyleEventArgs(name, mainPart, styleType));
					return name;
				}

				if (styleType == StyleValues.Character && !style.Type.Equals<StyleValues>(StyleValues.Character))
				{
					LinkedStyle linkStyle = style.GetFirstChild<LinkedStyle>();
					if (linkStyle != null) return linkStyle.Val;
				}
				return style.StyleId;
			}
		}

		#endregion

		#region DoesStyleExists

		/// <summary>
		/// Gets whether the given style exists in the document.
		/// </summary>
		public bool DoesStyleExists(string name)
		{
			return knownStyles.ContainsKey(name);
		}

		#endregion

		#region AddStyle

		/// <summary>
		/// Add a new style inside the document and refresh the style cache.
		/// </summary>
		public void AddStyle(String name, Style style)
		{
			knownStyles[name] = style;
			if (mainPart.StyleDefinitionsPart == null)
				mainPart.AddNewPart<StyleDefinitionsPart>().Styles = new Styles();
			mainPart.StyleDefinitionsPart.Styles.Append(style);
		}

        #endregion

        #region EnsureKnownStyle

        /// <summary>
        /// Ensure the specified style exists in the document.
        /// </summary>
        internal void EnsureKnownStyle(KnownStyles styleName)
        {
            if (styleName == KnownStyles.Hyperlink)
            {
                if (!this.DoesStyleExists("Hyperlink"))
                {
                    this.AddStyle("Hyperlink", new Style(
                        new StyleName() { Val = "Hyperlink" },
                        new UnhideWhenUsed(),
                        new StyleRunProperties(PredefinedStyles.HyperLink)
                    ) { Type = StyleValues.Character, StyleId = "Hyperlink" });
                }
            }
            else if (styleName == KnownStyles.Caption)
            {
                if (this.DoesStyleExists("caption"))
                    return;

                String normalStyleName = this.GetStyle("Normal", StyleValues.Paragraph);
                Style style = new Style(
                    new StyleName { Val = "caption" },
                    new BasedOn { Val = normalStyleName },
                    new NextParagraphStyle { Val = normalStyleName },
                    new UnhideWhenUsed(),
                    new PrimaryStyle(),
                    new StyleParagraphProperties
                    {
                        SpacingBetweenLines = new SpacingBetweenLines { Line = "240", LineRule = LineSpacingRuleValues.Auto }
                    },
                    new StyleRunProperties(PredefinedStyles.Caption)
                ) { Type = StyleValues.Paragraph, StyleId = "Caption" };

                this.AddStyle("caption", style);
            }
        }

        #endregion

        //____________________________________________________________________
        //

        public RunStyleCollection Runs
		{
			get { return runStyle; }
		}
        public TableStyleCollection Tables
		{
			get { return tableStyle; }
		}
        public ParagraphStyleCollection Paragraph
		{
			get { return paraStyle; }
		}
        public NumberingListStyleCollection NumberingList
        {
			// use lazy loading to avoid injecting NumberListDefinition if not required
            get { return listStyle ?? (listStyle = new NumberingListStyleCollection(mainPart)); }
        }

		//____________________________________________________________________
		//

		/// <summary>
		/// Gets the default StyleId to apply on the any new paragraph.
		/// </summary>
		internal String DefaultParagraphStyle
		{
			get { return paraStyle.DefaultParagraphStyle; }
			set { paraStyle.DefaultParagraphStyle = value; }
		}

		/// <summary>
		/// Gets or sets the default paragraph style to apply on any new runs.
		/// </summary>
		public String DefaultStyle
		{
			get { return DefaultParagraphStyle ?? runStyle.DefaultRunStyle; }
			set
			{
				if (String.IsNullOrEmpty(value))
				{
					runStyle.DefaultRunStyle = null;
					this.DefaultParagraphStyle = null;
					return;
				}

				Style s;
				if (!knownStyles.TryGetValue(value, out s))
				{
					this.DefaultParagraphStyle = value;
				}
				else
				{
					if (s.Type.Equals<StyleValues>(StyleValues.Paragraph))
						this.DefaultParagraphStyle = s.StyleId;
					else
						runStyle.DefaultRunStyle = s.StyleId;
				}
			}
		}

        /// <summary>
        /// Gets or sets the beginning and ending characters used in the &lt;q&gt; tag.
        /// </summary>
        public QuoteChars QuoteCharacters { get; set; }
	}
}