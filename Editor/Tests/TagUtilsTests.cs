using System.Collections.Generic;
using ModelLibrary.Editor.Utils;
using NUnit.Framework;

namespace ModelLibrary.Editor.Tests
{
    /// <summary>
    /// Tests for the TagUtils shared tag list operations.
    /// </summary>
    public class TagUtilsTests
    {
        [Test]
        public void TryAddTag_TrimsAndAdds()
        {
            List<string> tags = new List<string>();
            bool added = TagUtils.TryAddTag(tags, "  medieval  ", out string errorMessage);

            Assert.IsTrue(added);
            Assert.IsNull(errorMessage);
            Assert.AreEqual(1, tags.Count);
            Assert.AreEqual("medieval", tags[0]);
        }

        [Test]
        public void TryAddTag_RejectsDuplicateCaseInsensitive()
        {
            List<string> tags = new List<string> { "Weapon" };
            bool added = TagUtils.TryAddTag(tags, "weapon", out string errorMessage);

            Assert.IsFalse(added);
            Assert.IsNotNull(errorMessage);
            Assert.AreEqual(1, tags.Count);
        }

        [Test]
        public void ContainsTag_IsCaseInsensitive()
        {
            List<string> tags = new List<string> { "sci-fi" };

            Assert.IsTrue(TagUtils.ContainsTag(tags, "Sci-Fi"));
            Assert.IsFalse(TagUtils.ContainsTag(tags, "fantasy"));
        }

        [Test]
        public void TryAddTag_RejectsEmptyAndWhitespace()
        {
            List<string> tags = new List<string>();

            Assert.IsFalse(TagUtils.TryAddTag(tags, "", out string emptyError));
            Assert.IsNotNull(emptyError);

            Assert.IsFalse(TagUtils.TryAddTag(tags, "   ", out string whitespaceError));
            Assert.IsNotNull(whitespaceError);
            Assert.AreEqual(0, tags.Count);
        }

        [Test]
        public void FormatBadgeLabel_IncludesEmojiPrefix()
        {
            string label = TagUtils.FormatBadgeLabel("weapon");

            Assert.IsTrue(label.Contains("weapon"));
            Assert.IsTrue(label.StartsWith(UIConstants.TAG_BADGE_EMOJI));
        }
    }
}
