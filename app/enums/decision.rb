module Enums
    class Decision < EnumerateIt::Base

        ## Enumeration
        associate_values(
            win: [1, 'W'],
            loss: [2, 'L'],
			otl: [3, 'L-OT'],
			sol: [4, 'L-SO']
        )

		## Helpers
		def self.parse(str)
			self.enumeration.each do |key, value|

				# return integer of matching translation
				return value[0] if value[1] == str
			end
		end
    end
end
