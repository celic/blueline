module Enums
  class GoalieRecord < EnumerateIt::Base

    ## Enumeration
    associate_values(
      win: [1, 'W'],
      loss: [2, 'L'],
      ot: [3, 'L-OT'],
      pulled: [4, 'P']
    )

    ## Helpers
    def self.parse(str)
      self.enumeration.each do |key, value|
        return value[0] if value[1] == str
      end
    end
  end
end
