# == Schema Information
#
# Table name: games
#
#  id      :integer          not null, primary key
#  home_id :integer
#  away_id :integer
#  date    :date
#
# Indexes
#
#  index_games_on_away_id  (away_id)
#  index_games_on_home_id  (home_id)
#

module API
	module Entities
		class Game < Grape::Entity

			expose :id
			expose :home, using: API::Entities::Team
			expose :home_stats

			expose :away, using: API::Entities::Team
			expose :away_stats

			expose :date

		end
	end
end
