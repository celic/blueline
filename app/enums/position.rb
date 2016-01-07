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
            [ self::LW, self::C, self::RW ]
        end

		## Helpers
        def self.parse(str)
            self.value_for(str)
        end

		def self.forward?(pos)
			self.forwards.include? pos
		end

		def self.defenseman?(pos)
			pos == self::D
		end

		def self.goalie?(pos)
			pos == self::G
		end
    end
end
