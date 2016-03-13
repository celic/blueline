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

    class << self
      def forwards
        [LW, C, RW]
      end

      def parse(str)
        value_for(str.upcase)
      end

      def forward?(pos)
        forwards.include? pos
      end

      def defenseman?(pos)
        pos == D
      end

      def goalie?(pos)
        pos == G
      end
    end
  end
end
