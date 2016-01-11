module API
	module Entities
		class Game < Grape::Entity

			expose :home, :home_stats, :away, :away_stats

			expose :date

		end
	end
end
