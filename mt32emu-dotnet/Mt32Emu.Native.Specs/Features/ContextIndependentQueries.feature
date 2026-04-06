Feature: Context-independent queries
  As a consumer of Mt32Emu.Native
  I need context-independent functions to return sensible values without crashing
  So that I can trust the library on all platforms

  Scenario: Supported report handler version is returned
    When I call GetSupportedReportHandlerVersion
    Then the report handler version should be a defined enum value

  Scenario: Supported MIDI receiver version is returned
    When I call GetSupportedMidiReceiverVersion
    Then the MIDI receiver version should be a defined enum value

  Scenario Outline: Stereo output sample rate for each analog output mode
    When I query the stereo output sample rate for analog output mode "<mode>"
    Then the sample rate should be greater than 0

    Examples:
      | mode         |
      | DigitalOnly  |
      | Coarse       |
      | Accurate     |
      | Oversampled  |

  Scenario: Best analog output mode for 44100 Hz
    When I query the best analog output mode for sample rate 44100
    Then a valid analog output mode should be returned

  Scenario: Machine IDs are enumerable
    When I query the count of machine IDs
    Then the count should be greater than 0

  Scenario: ROM IDs are enumerable for a known machine
    When I query the count of ROM IDs for the first known machine
    Then the count should be greater than 0

  Scenario: Identifying a non-existent ROM file returns an error
    When I try to identify the ROM file "nonexistent_file.rom"
    Then the return code should be negative
