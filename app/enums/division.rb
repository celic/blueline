module Enums
  class Division < EnumerateIt::Base

    ## Enumeration
    associate_values(
      metro: [1, 'Metropolitan'],
      atlantic: [2, 'Atlantic'],
      pacific: [3, 'Pacific'],
      central: [4, 'Central']
    )

    ## Constants
    EAST = [METRO, ATLANTIC]
    WEST = [PACIFIC, CENTRAL]

    ## Helpers
    def self.east?(division)
      EAST.include?(division)
    end

    def self.west?(division)
      WEST.include?(division)
    end
  end
end
