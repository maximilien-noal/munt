Feature: Context lifecycle
  As a consumer of Mt32Emu.Native
  I need to create and destroy emulation contexts without crashes
  So that I can safely manage the synthesiser lifetime

  Scenario: Creating and freeing a context does not crash
    When I create a new emulation context
    Then the context handle should not be zero
    And I free the context without error

  Scenario: An unopened synth reports it is not open
    Given I have a fresh emulation context
    When I check if the synth is open
    Then it should report not open

  Scenario: Opening a synth without ROMs returns MissingRoms
    Given I have a fresh emulation context
    When I try to open the synth
    Then the return code should be MissingRoms

  Scenario: ROM info on a fresh context returns null pointers
    Given I have a fresh emulation context
    When I query the ROM info
    Then all ROM info fields should be zero
