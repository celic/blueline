module Enums
  class Position < EnumerateIt::Base

    ## Enumeration
    associate_values(
      lw: [1, 'Left Wing'],
      c: [2, 'Center'],
      rw: [3, 'Right Wing'],
      d: [4, 'Defenseman'],
      g: [5, 'Goalie']
    )

    ## Class Methods
    def self.forwards
      [LW, C, RW]
    end

    ## Helpers
    def self.parse(str)
      value_for(str)
    end

    def self.forward?(pos)
      forwards.include? pos
    end

    def self.defenseman?(pos)
      pos == D
    end

    def self.goalie?(pos)
      pos == G
    end
  end
end
