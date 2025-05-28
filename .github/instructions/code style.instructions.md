---
applyTo: '**/*.cs'
---
Formatting and style standards for this C# project:

## For writing code:

- 4-space indentation.
- Always use a blank line between code blocks (methods, properties, constructors, etc).
- Always add a blank line before if, for, foreach, and return.
- Always add a blank line after opening or before closing braces.
- Use expression-bodied members and start a new line before =>
- Constant names in UPPER_CASE.
- Use var when the type is obvious.
- XMLDoc is mandatory for public classes and methods.
- Non-inheritable classes must be sealed.
- DRY: avoid code repetition.
- Write code always in english.
- Use modern collections and initializers ([] and [..] when possible).
- <inherited /> for inherited members in XMLDoc.
- Always put using before namespace declaration and sort them alphabetically.
- Use `nameof` operator instead of hardcoded strings for member names.
- Make anonymous function static when possible.

## For writing tests:

- Use xUnit, Shouldly and NSubstitute.
- Place ExcludeFromCodeCoverage attribute on all test classes.
- All test classes must be public sealed.
- Use AAA (Arrange, Act, Assert) pattern.
- Use Method_Condition_ExpectedResult naming convention for test methods.
- Declare all dependencies as readonly fields and initialize it inline.
- Declare string const as const and use UPPER_CASE for names
- Use raw string to declare multi-line strings
- Avoid using magic strings and numbers, declare them as constants.
- Never try to mock non virtual classes
- If you can't mock a class, use a real instance of it or extract it to an interface.
- Use verbatim strings for file paths and other strings that require escaping.

## For writing documentation:

- Use Markdown for documentation.
- Use proper headings and subheadings.
- Use bullet points and numbered lists for clarity.
- Use code blocks for code snippets.
- Use links for references and further reading.
- Keep documentation up to date with code changes.
- Write allways in english.


