# == Schema Information
#
# Table name: teams
#
#  id           :integer          not null, primary key
#  name         :string
#  abbreviation :string
#  city         :string
#  state        :string
#  full_name    :string
#  division_id  :integer
#  wins         :integer          default(0)
#  losses       :integer          default(0)
#  ot           :integer          default(0)
#  so           :integer          default(0)
#  points       :integer          default(0)
#
# Indexes
#
#  index_teams_on_abbreviation  (abbreviation)
#

module API
	module Entities
		class Team < Grape::Entity

			expose :id, :abbreviation, :full_name, :name, :city, :state, :wins, :division_id, :wins, :losses, :ot, :sow, :sol, :points

			expose :players, using: API::Entities::Player, if: :roster

			expose :games, if: :recent do |team, opts|

				# done this way to prevent option inheritance
				API::Entities::Game.represent team.games.order(date: :desc).limit(5), Hash.new
			end
		end
	end
end
