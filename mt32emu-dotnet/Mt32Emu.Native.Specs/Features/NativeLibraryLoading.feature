Feature: Native library loading
  As a consumer of the Mt32Emu.Native NuGet package
  I need the native mt32emu library to load without crashing
  So that I can use the synthesiser on any supported platform

  Scenario: The native library loads and returns a version integer
    When I call GetLibraryVersionInt
    Then the result should be a positive integer

  Scenario: The native library returns a valid version string
    When I call GetLibraryVersionString
    Then the result should match the pattern "\d+\.\d+\.\d+"

  Scenario: The version integer and string are consistent
    When I call GetLibraryVersionInt
    And I call GetLibraryVersionString
    Then the version string and integer should represent the same version
