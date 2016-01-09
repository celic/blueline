module Enums
    class Division < EnumerateIt::Base

        ## Enumeration
        associate_values(
            metro: [1, 'Metropolitan'],
            atlantic: [2, 'Atlantic'],
			pacific: [3, 'Pacific'],
			central: [4, 'Central'],
        )

		## Constants
		EAST = [ self::METRO, self::ATLANTIC ]
		WEST = [ self::PACIFIC, self::CENTRAL ]

		## Helpers
		def east?(division)
			self::EAST.include? division
		end

		def west?(division)
			self::WEST.include? division
		end
	end
end
