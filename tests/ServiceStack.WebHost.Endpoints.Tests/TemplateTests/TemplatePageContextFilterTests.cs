using NUnit.Framework;
using ServiceStack.Templates;
using ServiceStack.Testing;
using ServiceStack.VirtualPath;

#if NETCORE
using Microsoft.Extensions.Primitives;
#endif

namespace ServiceStack.WebHost.Endpoints.Tests.TemplateTests
{
    public class TemplatePageContextFilterTests
    {
        [Test]
        public void Can_pass_variables_into_partials()
        {
            var context = new TemplatePagesContext
            {
                Args = { ["defaultMessage"] = "this is the default message" }
            }.Init();

            context.VirtualFiles.WriteFile("_layout.html", @"
<html>
  <title>{{ title }}</title>
</head>
<body>
{{ 'header' | partial({ id: 'the-page', message: 'in your header' }) }}
{{ page }}
</body>");

            context.VirtualFiles.WriteFile("header.html", @"
<header id='{{ id | otherwise('header') }}'>
  {{ message | otherwise(defaultMessage) }}
</header>");

            context.VirtualFiles.WriteFile("page.html", @"<h1>{{ title }}</h1>");

            var result = new PageResult(context.GetPage("page")) 
            {
                Args = { ["title"] = "The title" }
            }.Result;
            Assert.That(result.SanitizeNewLines(), Is.EqualTo(@"
<html>
  <title>The title</title>
</head>
<body>
<header id='the-page'>
  in your header
</header>
<h1>The title</h1>
</body>
".SanitizeNewLines()));            
        }

        [Test]
        public void Can_load_page_with_partial_and_scoped_variables()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["myPartial"] = "my-partial"
                }
            }.Init();

            context.VirtualFiles.WriteFile("_layout.html", @"
<html>
  <title>{{ title }}</title>
</head>
<body>
{{ 'my-partial' | partial({ title: 'with-partial', tag: 'h2' }) }}
{{ myPartial | partial({ title: 'with-partial-binding', tag: 'h2' }) }}
<footer>{{ title }}</footer>
</body>");
            
            context.VirtualFiles.WriteFile("my-partial.html", "<{{ tag }}>{{ title }}</{{ tag }}>");
            
            var result = new PageResult(context.GetPage("my-partial"))
            {
                Args = { ["title"] = "The title" }
            }.Result;
            Assert.That(result.SanitizeNewLines(), Is.EqualTo(@"
<html>
  <title>The title</title>
</head>
<body>
<h2>with-partial</h2>
<h2>with-partial-binding</h2>
<footer>The title</footer>
</body>
".SanitizeNewLines()));
        }

        [Test]
        public void Can_load_page_with_page_or_partial_with_scoped_variables_containing_bindings()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["myPartial"] = "my-partial",
                    ["headingTag"] = "h2",
                }
            }.Init();

            context.VirtualFiles.WriteFile("_layout.html", @"
<html>
  <title>{{ title }}</title>
</head>
<body>
{{ 'my-partial' | partial({ title: title, tag: headingTag }) }}
{{ myPartial | partial({ title: partialTitle, tag: headingTag }) }}
</body>");
            
            context.VirtualFiles.WriteFile("my-partial.html", "<{{ tag }}>{{ title }}</{{ tag }}>");
            
            var result = new PageResult(context.GetPage("my-partial"))
            {
                Args =
                {
                    ["title"] = "The title",
                    ["partialTitle"] = "Partial Title",
                }
            }.Result;
            Assert.That(result.SanitizeNewLines(), Is.EqualTo(@"
<html>
  <title>The title</title>
</head>
<body>
<h2>The title</h2>
<h2>Partial Title</h2>
</body>
".SanitizeNewLines()));
        }

        [Test]
        public void Does_replace_bindings()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["contextTitle"] = "The title",
                    ["contextPartial"] = "bind-partial",
                    ["contextTag"] = "h2",
                    ["a"] = "foo",
                    ["b"] = "bar",
                }
            }.Init();

            context.VirtualFiles.WriteFile("_layout.html", @"
<html>
  <title>{{ title }}</title>
</head>
<body>
{{ contextPartial | partial({ title: contextTitle, tag: contextTag, items: [a,b] }) }}
{{ page }}
</body>");
            
            context.VirtualFiles.WriteFile("bind-partial.html", @"
<{{ tag }}>{{ title | upper }}</{{ tag }}>
<p>{{ items | join(', ') }}</p>");
            
            context.VirtualFiles.WriteFile("bind-page.html", @"
<section>
{{ pagePartial | partial({ tag: pageTag, items: items }) }}
</section>
");
            
            var result = new PageResult(context.GetPage("bind-page"))
            {
                Args =
                {
                    ["title"] = "Page title",
                    ["pagePartial"] = "bind-partial",
                    ["pageTag"] = "h3",
                    ["items"] = new[] { 1, 2, 3 },
                }
            }.Result;

            Assert.That(result.SanitizeNewLines(), Is.EqualTo(@"
<html>
  <title>Page title</title>
</head>
<body>
<h2>THE TITLE</h2>
<p>foo, bar</p>
<section>
<h3>PAGE TITLE</h3>
<p>1, 2, 3</p>
</section>

</body>
".SanitizeNewLines()));

        }

        [Test]
        public void Can_repeat_templates_using_forEach()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["letters"] = new[]{ "A", "B", "C" },
                    ["numbers"] = new[]{ 1, 2, 3 },
                }
            };
            
            Assert.That(new PageResult(context.OneTimePage("<ul> {{ '<li> {{it}} </li>' | forEach(letters) }} </ul>")).Result,
                Is.EqualTo("<ul> <li> A </li><li> B </li><li> C </li> </ul>"));

            Assert.That(new PageResult(context.OneTimePage("<ul> {{ '<li> {{it}} </li>' | forEach(numbers) }} </ul>")).Result,
                Is.EqualTo("<ul> <li> 1 </li><li> 2 </li><li> 3 </li> </ul>"));
        }

        [Test]
        public void Can_repeat_templates_using_forEach_in_page_and_layouts()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["numbers"] = new[]{ 1, 2, 3 },
                }
            };
            
            context.VirtualFiles.WriteFile("_layout.html", @"
<html>
<body>
<header>
<ul> {{ '<li> {{it}} </li>' | forEach(numbers) }} </ul>
</header>
<section>
{{ page }}
</section>
</body>
</html>
");
            context.VirtualFiles.WriteFile("page.html", "<ul> {{ '<li> {{it}} </li>' | forEach(letters) }} </ul>");
            
            var result = new PageResult(context.GetPage("page"))
            {
                Args =
                {
                    ["letters"] = new[]{ "A", "B", "C" },
                }
            }.Result;
            
            Assert.That(result.SanitizeNewLines(),
                Is.EqualTo(@"
<html>
<body>
<header>
<ul> <li> 1 </li><li> 2 </li><li> 3 </li> </ul>
</header>
<section>
<ul> <li> A </li><li> B </li><li> C </li> </ul>
</section>
</body>
</html>"
                    .SanitizeNewLines()));
        }

        [Test]
        public void Can_repeat_templates_with_bindings_using_forEach()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["items"] = new[]
                    {
                        new ModelBinding { Object = new NestedModelBinding { Prop = "A" }}, 
                        new ModelBinding { Object = new NestedModelBinding { Prop = "B" }}, 
                        new ModelBinding { Object = new NestedModelBinding { Prop = "C" }}, 
                    },
                }
            };
            
            Assert.That(new PageResult(context.OneTimePage("<ul> {{ '<li> {{ it.Object.Prop }} </li>' | forEach(items) }} </ul>")).Result,
                Is.EqualTo("<ul> <li> A </li><li> B </li><li> C </li> </ul>"));
        }

        [Test]
        public void Can_repeat_templates_with_bindings_and_custom_scope_using_forEach()
        {
            var context = new TemplatePagesContext
            {
                Args =
                {
                    ["items"] = new[]
                    {
                        new ModelBinding { Object = new NestedModelBinding { Prop = "A" }}, 
                        new ModelBinding { Object = new NestedModelBinding { Prop = "B" }}, 
                        new ModelBinding { Object = new NestedModelBinding { Prop = "C" }}, 
                    },
                }
            };
            
            Assert.That(new PageResult(context.OneTimePage("<ul> {{ '<li> {{ item.Object.Prop }} </li>' | forEach(items, 'item') }} </ul>")).Result,
                Is.EqualTo("<ul> <li> A </li><li> B </li><li> C </li> </ul>"));
        }

        [Test]
        public void Can_use_forEach_with_markdown()
        {
            using (new BasicAppHost().Init())
            {
                var context = new TemplatePagesFeature
                {
                    Args =
                    {
                        ["items"] = new[]{ "foo", "bar", "qux" }
                    }
                }.Init();
             
                Assert.That(new PageResult(context.OneTimePage("{{ ' - {{it}}\n' | forEach(items) | markdown }}")).Result.RemoveAllWhitespace(), 
                    Is.EqualTo("<ul><li>foo</li><li>bar</li><li>qux</li></ul>".RemoveAllWhitespace()));
            }
        }
    }
}