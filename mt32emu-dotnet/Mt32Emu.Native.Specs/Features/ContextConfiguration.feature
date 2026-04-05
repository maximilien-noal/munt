Feature: Context configuration
  As a consumer of Mt32Emu.Native
  I need to set and get synthesiser properties on a context without crashes
  So that I can configure the emulation engine safely on any platform

  Scenario: Default renderer type is Bit16S
    Given I have a fresh emulation context
    When I query the selected renderer type
    Then the renderer type should be Bit16S

  Scenario Outline: Setting and getting renderer type round-trips
    Given I have a fresh emulation context
    When I set the renderer type to "<type>"
    And I query the selected renderer type
    Then the renderer type should be <type>

    Examples:
      | type    |
      | Bit16S  |
      | Float   |

  Scenario: Setting and getting output gain round-trips
    Given I have a fresh emulation context
    When I set the output gain to 1.5
    And I query the output gain
    Then the output gain should be approximately 1.5

  Scenario: Setting and getting reverb output gain round-trips
    Given I have a fresh emulation context
    When I set the reverb output gain to 0.75
    And I query the reverb output gain
    Then the reverb output gain should be approximately 0.75

  Scenario Outline: Setting and getting DAC input mode round-trips
    Given I have a fresh emulation context
    When I set the DAC input mode to "<mode>"
    And I query the DAC input mode
    Then the DAC input mode should be <mode>

    Examples:
      | mode        |
      | Nice        |
      | Pure        |
      | Generation1 |
      | Generation2 |

  Scenario Outline: Setting and getting MIDI delay mode round-trips
    Given I have a fresh emulation context
    When I set the MIDI delay mode to "<mode>"
    And I query the MIDI delay mode
    Then the MIDI delay mode should be <mode>

    Examples:
      | mode                     |
      | Immediate                |
      | DelayShortMessagesOnly   |
      | DelayAll                 |

  Scenario: Setting partial count does not crash
    Given I have a fresh emulation context
    When I set the partial count to 64
    Then no exception should be thrown

  Scenario: Setting analog output mode does not crash
    Given I have a fresh emulation context
    When I set the analog output mode to "Accurate"
    Then no exception should be thrown

  Scenario: Setting stereo output sample rate does not crash
    Given I have a fresh emulation context
    When I set the stereo output sample rate to 48000.0
    Then no exception should be thrown

  Scenario: Setting sample rate conversion quality does not crash
    Given I have a fresh emulation context
    When I set the sample rate conversion quality to "Best"
    Then no exception should be thrown

  Scenario: Setting reverb enabled does not crash on unopened synth
    Given I have a fresh emulation context
    When I set reverb enabled to true
    And I query if reverb is enabled
    Then no exception should be thrown

  Scenario: Setting reversed stereo does not crash on unopened synth
    Given I have a fresh emulation context
    When I set reversed stereo to true
    And I query if reversed stereo is enabled
    Then no exception should be thrown

  Scenario: MIDI event queue size can be set
    Given I have a fresh emulation context
    When I set the MIDI event queue size to 1024
    Then no exception should be thrown

  Scenario: Master volume override round-trips
    Given I have a fresh emulation context
    When I set the master volume override to 100
    And I query the master volume override
    Then the master volume override should be 100
