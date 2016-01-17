module Enums
    class Decision < EnumerateIt::Base

        ## Enumeration
        associate_values(
            win: [1, 'W', 2],
            loss: [2, 'L', 0],
			otl: [3, 'L-OT', 1],
			sol: [4, 'L-SO', 1],
            sow: [5, 'W-SO', 2]
        )

		## Helpers
		def self.parse(str)
			self.enumeration.each do |key, value|

				# return value of matching translation
				return value[0] if value[1] == str
			end
		end

		def self.points_for(decision)

			self.enumeration.each do |key, value|

				# return points of matching value
				return value[2] if value[0] == decision
			end
		end
    end
end
