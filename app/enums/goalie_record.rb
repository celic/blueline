module Enums
    class GoalieRecord < EnumerateIt::Base

        ## Enumeration
        associate_values(
            win: [1, 'W'],
            loss: [2, 'L'],
			ot: [3, 'L-OT'],
			pulled: [4, ''],
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
